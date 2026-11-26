using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;
using Aida.Engine.Ontology;
using Aida.Platform.Keys;
using Aida.Platform.Packs;
using Aida.Platform.People;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Actions;

public sealed class AgendaToolTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    private sealed class ToolSession : IDisposable
    {
        public AgendaWork Work { get; }

        public AgendaTool Tool { get; }

        public ToolSession(AgendaWork work, AgendaTool tool)
        {
            Work = work;
            Tool = tool;
        }

        public void Dispose() => Work.Dispose();
    }

    private sealed class ToolContext : IDisposable
    {
        public string DirectoryPath { get; }

        public PlatformDatabase Database { get; }

        public AgendaSetup Setup { get; }

        public PersonCatalog Catalog { get; }

        public ManualTime Time { get; }

        public MachineKey Key { get; }

        public Sealer Sealer { get; }

        public PersonKeys PersonKeys { get; }

        public ToolContext(AgendaSetup? setup = null, DateTimeOffset? initialTime = null)
        {
            var repoRoot = FindRepoRoot();
            var consultorioPath = Path.Combine(repoRoot, "packs", "agenda", "consultorio", "agenda.yaml");
            Setup = setup ?? AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(consultorioPath), consultorioPath);
            Catalog = PersonCatalog.Load(Path.Combine(repoRoot, "packs", "platform", "people.yaml"));

            DirectoryPath = Path.Combine(Path.GetTempPath(), "aida-tool-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(DirectoryPath);
            Database = new PlatformDatabase(Path.Combine(DirectoryPath, "aida.db"));

            Key = MachineKey.OpenOrCreate(Path.Combine(DirectoryPath, "machine.key"), new DevelopmentKeyProtector());
            Sealer = new Sealer(Key);
            PersonKeys = PersonKeys.From(Key);

            Database.ApplyArmSchema(AgendaDeclarations.ArmId, AgendaSchema.Steps(Sealer, PersonKeys));

            Time = new ManualTime();
            Time.Now = initialTime ?? DateTimeOffset.Parse("2026-11-16T07:30:00-05:00");
        }

        public ToolSession Begin()
        {
            var work = AgendaWork.Begin(Database, Sealer, PersonKeys);
            var tool = new AgendaTool(work.Store, work.People, Setup, Catalog, Time);
            return new ToolSession(work, tool);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, true);
                }
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void Agendar_missing_client()
    {
        using var ctx = new ToolContext();
        using var session = ctx.Begin();

        var r1 = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "",
            ["phone"] = "123",
            ["service"] = "control",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Rejected, r1.Status);
        Assert.Equal("missing_client", r1.Reason);

        var r2 = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "   ",
            ["service"] = "control",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Rejected, r2.Status);
        Assert.Equal("missing_client", r2.Reason);
    }

    [Fact]
    public void Agendar_unknown_service()
    {
        using var ctx = new ToolContext();
        using var session = ctx.Begin();

        var r = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "123",
            ["service"] = "servicio_fantasma",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Rejected, r.Status);
        Assert.Equal("unknown_service", r.Reason);
    }

    [Theory]
    [InlineData("2026-11-16T08:00:00")] // Sin desfase
    [InlineData("2026-11-16T08:00-05:00")] // Sin segundos
    [InlineData("")] // Vacío
    [InlineData("mañana")] // Texto libre
    public void Agendar_bad_argument(string badStart)
    {
        using var ctx = new ToolContext();
        using var session = ctx.Begin();

        var r = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "123",
            ["service"] = "control",
            ["start"] = badStart,
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Rejected, r.Status);
        Assert.Equal("bad_argument", r.Reason);
    }

    [Fact]
    public void Agendar_unknown_resource()
    {
        using var ctx = new ToolContext();
        using var session = ctx.Begin();

        var r = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "123",
            ["service"] = "control",
            ["resource"] = "consultorio_99",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Rejected, r.Status);
        Assert.Equal("unknown_resource", r.Reason);
    }

    [Fact]
    public void Agendar_service_not_offered()
    {
        using var ctx = new ToolContext();
        using var session = ctx.Begin();

        // En consultorio, consultorio_2 no ofrece 'primera_vez'
        var r = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "123",
            ["service"] = "primera_vez",
            ["resource"] = "consultorio_2",
            ["start"] = "2026-11-16T09:00:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Rejected, r.Status);
        Assert.Equal("service_not_offered", r.Reason);
    }

    [Fact]
    public void Agendar_past()
    {
        using var ctx = new ToolContext(initialTime: DateTimeOffset.Parse("2026-11-16T10:00:00-05:00"));
        using var session = ctx.Begin();

        var r = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "123",
            ["service"] = "control",
            ["start"] = "2026-11-16T09:00:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Rejected, r.Status);
        Assert.Equal("past", r.Reason);
    }

    [Fact]
    public void Agendar_outside_hours()
    {
        using var ctx = new ToolContext(initialTime: DateTimeOffset.Parse("2026-11-16T05:00:00-05:00"));
        using var session = ctx.Begin();

        // Fuera de horario: lunes a las 06:00
        var r = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "123",
            ["service"] = "control",
            ["start"] = "2026-11-16T06:00:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Rejected, r.Status);
        Assert.Equal("outside_hours", r.Reason);
    }

    [Fact]
    public void Solapes_en_los_bordes()
    {
        using var ctx = new ToolContext();
        using var session = ctx.Begin();

        // Agendamos de 08:00 a 08:30 (control dura 30m) en consultorio_1
        var r1 = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "111",
            ["service"] = "control",
            ["resource"] = "consultorio_1",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Done, r1.Status);

        // Agendar de 08:15 a 08:45 solapa con la anterior
        var rOverlap = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Beto",
            ["phone"] = "222",
            ["service"] = "control",
            ["resource"] = "consultorio_1",
            ["start"] = "2026-11-16T08:15:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Rejected, rOverlap.Status);
        Assert.Equal("overlap", rOverlap.Reason);

        // Agendar justo en el borde 08:30 a 09:00 NO solapa
        var rBorder = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Carlos",
            ["phone"] = "333",
            ["service"] = "control",
            ["resource"] = "consultorio_1",
            ["start"] = "2026-11-16T08:30:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Done, rBorder.Status);
    }

    [Fact]
    public void La_cancelada_no_ocupa()
    {
        using var ctx = new ToolContext();
        using var session = ctx.Begin();

        var rBook = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "111",
            ["service"] = "control",
            ["resource"] = "consultorio_1",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Done, rBook.Status);
        var appId = rBook.Data["appointment"];

        // Cancelamos la cita
        var rCancel = session.Tool.Execute(new ActionId("cancelar"), new Dictionary<string, string>
        {
            ["appointment"] = appId,
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Done, rCancel.Status);

        // El mismo hueco queda libre para agendar
        var rNew = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Beto",
            ["phone"] = "222",
            ["service"] = "control",
            ["resource"] = "consultorio_1",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Done, rNew.Status);
    }

    [Fact]
    public void Las_transiciones_solo_desde_booked()
    {
        using var ctx = new ToolContext();
        using var session = ctx.Begin();

        var rBook = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "111",
            ["service"] = "control",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["by"] = "u1",
        });
        var appId = rBook.Data["appointment"];

        session.Tool.Execute(new ActionId("cancelar"), new Dictionary<string, string>
        {
            ["appointment"] = appId,
            ["by"] = "u1",
        });

        // Intentar cancelar de nuevo
        var rCancelAgain = session.Tool.Execute(new ActionId("cancelar"), new Dictionary<string, string>
        {
            ["appointment"] = appId,
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Rejected, rCancelAgain.Status);
        Assert.Equal("not_booked", rCancelAgain.Reason);

        // Intentar marcar atendida o no asistió sobre una cancelada
        var rAttended = session.Tool.Execute(new ActionId("atendida"), new Dictionary<string, string>
        {
            ["appointment"] = appId,
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Rejected, rAttended.Status);
        Assert.Equal("not_booked", rAttended.Reason);
    }

    [Fact]
    public void Not_yet_antes_de_la_hora()
    {
        using var ctx = new ToolContext(initialTime: DateTimeOffset.Parse("2026-11-16T07:30:00-05:00"));
        using var session = ctx.Begin();

        var rBook = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "111",
            ["service"] = "control",
            ["start"] = "2026-11-16T09:00:00-05:00",
            ["by"] = "u1",
        });
        var appId = rBook.Data["appointment"];

        // Reloj a las 08:00, cita a las 09:00: todavía no se puede marcar atendida ni no asistió
        ctx.Time.Now = DateTimeOffset.Parse("2026-11-16T08:00:00-05:00");

        var rAttended = session.Tool.Execute(new ActionId("atendida"), new Dictionary<string, string>
        {
            ["appointment"] = appId,
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Rejected, rAttended.Status);
        Assert.Equal("not_yet", rAttended.Reason);

        var rNoShow = session.Tool.Execute(new ActionId("no_asistio"), new Dictionary<string, string>
        {
            ["appointment"] = appId,
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Rejected, rNoShow.Status);
        Assert.Equal("not_yet", rNoShow.Reason);

        // Reloj a las 09:00: ya se puede marcar
        ctx.Time.Now = DateTimeOffset.Parse("2026-11-16T09:00:00-05:00");
        var rOk = session.Tool.Execute(new ActionId("atendida"), new Dictionary<string, string>
        {
            ["appointment"] = appId,
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Done, rOk.Status);
    }

    [Fact]
    public void Reprogramar_no_choca_consigo_misma()
    {
        using var ctx = new ToolContext();
        using var session = ctx.Begin();

        var rBook = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "111",
            ["service"] = "control",
            ["resource"] = "consultorio_1",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["by"] = "u1",
        });
        var appId = rBook.Data["appointment"];

        // Reprogramar a la misma hora exacta
        var rSame = session.Tool.Execute(new ActionId("reprogramar"), new Dictionary<string, string>
        {
            ["appointment"] = appId,
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Done, rSame.Status);
    }

    [Fact]
    public void Sin_recurso_toma_el_primero_libre()
    {
        using var ctx = new ToolContext();
        using var session = ctx.Begin();

        // Ocupamos consultorio_1 a las 09:00 (a las 09:00 consultorio_2 también está abierto)
        session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "111",
            ["service"] = "control",
            ["resource"] = "consultorio_1",
            ["start"] = "2026-11-16T09:00:00-05:00",
            ["by"] = "u1",
        });

        // Agendar sin especificar recurso toma consultorio_2 (el primero libre)
        var r = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Beto",
            ["phone"] = "222",
            ["service"] = "control",
            ["start"] = "2026-11-16T09:00:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Done, r.Status);
        var appId = long.Parse(r.Data["appointment"]);
        var app = session.Work.Store.Get(appId);
        Assert.Equal("consultorio_2", app!.Resource);
    }

    [Fact]
    public void Plantillas_consultorio_y_peluqueria()
    {
        var repoRoot = FindRepoRoot();
        var peluqueriaPath = Path.Combine(repoRoot, "packs", "agenda", "peluqueria", "agenda.yaml");
        var setupPeluqueria = AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(peluqueriaPath), peluqueriaPath);

        using var ctx = new ToolContext(setup: setupPeluqueria, initialTime: DateTimeOffset.Parse("2026-11-21T07:00:00-05:00"));
        using var session = ctx.Begin();

        var r = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Cliente Uno",
            ["phone"] = "3011110000",
            ["service"] = "corte",
            ["start"] = "2026-11-21T08:00:00-05:00",
            ["by"] = "u1",
        });
        Assert.Equal(ToolStatus.Done, r.Status);
    }

    [Fact]
    public void Por_telefono_conocido_con_otro_nombre_reescribe_nombre_y_telefono()
    {
        using var ctx = new ToolContext();
        using var session = ctx.Begin();

        session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana Pérez",
            ["phone"] = "300 555 0142",
            ["service"] = "control",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["by"] = "u1",
        });

        // Misma persona por huella de dígitos, pero nombre y formato de teléfono actualizado
        session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
        {
            ["name"] = "Ana P.",
            ["phone"] = "3005550142",
            ["service"] = "control",
            ["start"] = "2026-11-16T09:00:00-05:00",
            ["by"] = "u1",
        });

        var client = session.Work.People.ByPhone("3005550142").FirstOrDefault();
        Assert.NotNull(client);
        Assert.Equal("Ana P.", client.Name);
        Assert.Equal("3005550142", client.Phone);
    }
}
