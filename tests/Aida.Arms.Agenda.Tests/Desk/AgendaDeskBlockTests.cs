using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;
using Aida.Engine.Ontology;
using Aida.Platform.Audit;
using Aida.Platform.Identity;
using Aida.Platform.Keys;
using Aida.Platform.Packs;
using Aida.Platform.People;
using Aida.Platform.Roles;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Desk;

public sealed class AgendaDeskBlockTests
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

    private sealed class Context : IDisposable
    {
        public string DirectoryPath { get; }
        public TestDatabase Db { get; }
        public ManualTime Time { get; }
        public AuditLog Audit { get; }
        public RoleCatalog Roles { get; }
        public UserStore Users { get; }
        public AgendaSetup Setup { get; }
        public ActionCatalog Catalog { get; }
        public MachineKey Key { get; }
        public AgendaDesk Desk { get; }
        public UserAccount Admin { get; }
        public UserAccount Reception { get; }
        public UserAccount Professional { get; }
        public UserAccount Cashier { get; }

        public Context()
        {
            var repoRoot = FindRepoRoot();
            DirectoryPath = Path.Combine(Path.GetTempPath(), "aida-desk-block-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(DirectoryPath);

            Db = new TestDatabase();
            Time = new ManualTime();
            Time.Now = DateTimeOffset.Parse("2026-11-16T07:30:00-05:00");

            Audit = new AuditLog(Db.Database, Time, AgendaDeclarations.AuditKinds);

            var rolesPath = Path.Combine(repoRoot, "packs", "platform", "roles.yaml");
            Roles = RoleCatalog.Load(rolesPath, AgendaDeclarations.Permissions);

            var hasher = new PasswordHasher(1);
            Users = new UserStore(Db.Database, Audit, Roles, hasher, Time);

            Admin = Users.CreateFirstAdministrator("admin", "Administradora", "PassAdmin123!", "127.0.0.1");
            Reception = Users.Create(Admin.Id, "127.0.0.1", "recep", "Recepcionista", "recepcion", "PassRecep123!");
            Professional = Users.Create(Admin.Id, "127.0.0.1", "doc", "Doctora Ana", "profesional", "PassDoc123!");
            Cashier = Users.Create(Admin.Id, "127.0.0.1", "caja", "Cajero", "caja", "PassCaja123!");

            var consultorioPath = Path.Combine(repoRoot, "packs", "agenda", "consultorio", "agenda.yaml");
            Setup = AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(consultorioPath), consultorioPath);

            var armPath = Path.Combine(repoRoot, "packs", "agenda", "arm.yaml");
            Catalog = AgendaManifest.Load(armPath).Actions;

            var people = PersonCatalog.Load(Path.Combine(repoRoot, "packs", "platform", "people.yaml"));

            var keyPath = Path.Combine(DirectoryPath, "machine.key");
            Key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());

            Desk = new AgendaDesk(Db.Database, Audit, Roles, Users, Setup, people, Catalog, Reversibility.Reversible, Key, Time);
        }

        public void Dispose()
        {
            Db.Dispose();
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
    public void CanBlock_segun_el_rol()
    {
        using var ctx = new Context();

        Assert.True(ctx.Desk.CanBlock(ctx.Admin));
        Assert.True(ctx.Desk.CanBlock(ctx.Reception));
        Assert.False(ctx.Desk.CanBlock(ctx.Professional));
        Assert.False(ctx.Desk.CanBlock(ctx.Cashier));
        Assert.False(ctx.Desk.CanBlock(null!));
    }

    [Fact]
    public void Profesional_bloquea_da_forbidden_sin_eventos_ni_fila()
    {
        using var ctx = new Context();

        var initialAuditCount = ctx.Audit.Lines().Count;

        var result = ctx.Desk.Submit(ctx.Professional, "127.0.0.1", "bloquear", new Dictionary<string, string>
        {
            ["resource"] = "consultorio_1",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["end"] = "2026-11-16T10:00:00-05:00",
            ["reason"] = "Intento por profesional"
        });

        Assert.Equal(ToolStatus.Rejected, result.Status);
        Assert.Equal("forbidden", result.Reason);

        // Sin eventos de la puerta ni eventos de auditoría nuevos
        Assert.Empty(ctx.Desk.EventLines());
        Assert.Equal(initialAuditCount, ctx.Audit.Lines().Count);

        // Sin fila de bloqueo en base de datos
        var day = ctx.Desk.Day(new DateOnly(2026, 11, 16));
        Assert.Empty(day.Blocks);
    }

    [Fact]
    public void Profesional_edita_se_hace()
    {
        using var ctx = new Context();

        // Recepción crea una cita
        var bookResult = ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Carlos",
            ["phone"] = "3001234567",
            ["service"] = "control",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["resource"] = "consultorio_1"
        });
        Assert.Equal(ToolStatus.Done, bookResult.Status);
        var appId = bookResult.Data["appointment"];

        // El profesional edita la cita (tiene agenda.manage)
        var editResult = ctx.Desk.Submit(ctx.Professional, "127.0.0.1", "editar", new Dictionary<string, string>
        {
            ["appointment"] = appId,
            ["name"] = "Carlos Alberto",
            ["phone"] = "3001234567"
        });
        Assert.Equal(ToolStatus.Done, editResult.Status);

        var day = ctx.Desk.Day(new DateOnly(2026, 11, 16));
        var appView = Assert.Single(day.Appointments);
        Assert.Equal("Carlos Alberto", appView.ClientName);
    }

    [Fact]
    public void Recepcion_bloquea_y_desbloquea_registra_agenda_changed_con_block()
    {
        using var ctx = new Context();

        // 1. Bloquear
        var blockResult = ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "bloquear", new Dictionary<string, string>
        {
            ["resource"] = "consultorio_1",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["end"] = "2026-11-16T10:00:00-05:00",
            ["reason"] = "Jornada limpieza"
        });
        Assert.Equal(ToolStatus.Done, blockResult.Status);
        var blockId = blockResult.Data["block"];

        var lines = ctx.Audit.Lines();
        var lastLine = lines[^1];
        using (var doc = System.Text.Json.JsonDocument.Parse(lastLine))
        {
            var root = doc.RootElement;
            Assert.Equal("agenda_changed", root.GetProperty("kind").GetString());
            var dataJson = root.GetProperty("data").GetString()!;
            using var dataDoc = System.Text.Json.JsonDocument.Parse(dataJson);
            var data = dataDoc.RootElement;
            Assert.Equal("bloquear", data.GetProperty("action").GetString());
            Assert.Equal("2026-11-16T08:00:00-05:00", data.GetProperty("start").GetString());
            Assert.Equal(blockId, data.GetProperty("block").GetString());
            Assert.False(data.TryGetProperty("appointment", out _));
        }
        Assert.Empty(ctx.Audit.Verify());

        // 2. Desbloquear
        var unblockResult = ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "desbloquear", new Dictionary<string, string>
        {
            ["block"] = blockId
        });
        Assert.Equal(ToolStatus.Done, unblockResult.Status);

        var linesAfter = ctx.Audit.Lines();
        var lastLineAfter = linesAfter[^1];
        using (var doc = System.Text.Json.JsonDocument.Parse(lastLineAfter))
        {
            var root = doc.RootElement;
            Assert.Equal("agenda_changed", root.GetProperty("kind").GetString());
            var dataJson = root.GetProperty("data").GetString()!;
            using var dataDoc = System.Text.Json.JsonDocument.Parse(dataJson);
            var data = dataDoc.RootElement;
            Assert.Equal("desbloquear", data.GetProperty("action").GetString());
            Assert.Equal(blockId, data.GetProperty("block").GetString());
        }
        Assert.Empty(ctx.Audit.Verify());
    }

    [Fact]
    public void Conflicts_devuelve_citas_en_orden_y_vacio_si_otro_motivo()
    {
        using var ctx = new Context();

        // Agendar cita 1 a las 09:00
        var b1 = ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Paciente 1", ["phone"] = "111", ["service"] = "control",
            ["start"] = "2026-11-16T09:00:00-05:00", ["resource"] = "consultorio_1"
        });
        // Agendar cita 2 a las 08:30
        var b2 = ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Paciente 2", ["phone"] = "222", ["service"] = "control",
            ["start"] = "2026-11-16T08:30:00-05:00", ["resource"] = "consultorio_1"
        });

        // Bloqueo que cubre ambas citas: se rechaza con has_appointments
        var blockResult = ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "bloquear", new Dictionary<string, string>
        {
            ["resource"] = "consultorio_1",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["end"] = "2026-11-16T10:00:00-05:00"
        });
        Assert.Equal(ToolStatus.Rejected, blockResult.Status);
        Assert.Equal("has_appointments", blockResult.Reason);

        var conflicts = ctx.Desk.Conflicts(blockResult);
        Assert.Equal(2, conflicts.Count);
        // Orden por inicio (08:30 antes que 09:00)
        Assert.Equal(long.Parse(b2.Data["appointment"]), conflicts[0].Appointment.Id);
        Assert.Equal(long.Parse(b1.Data["appointment"]), conflicts[1].Appointment.Id);
        Assert.Equal("Paciente 2", conflicts[0].ClientName);
        Assert.Equal("Paciente 1", conflicts[1].ClientName);

        // Si el resultado no es has_appointments, devuelve vacío
        var otherResult = ToolResult.Rejected("blocked");
        Assert.Empty(ctx.Desk.Conflicts(otherResult));
        Assert.Empty(ctx.Desk.Conflicts(ToolResult.Done(new Dictionary<string, string>())));
    }

    [Fact]
    public void Day_trae_los_bloqueos_del_dia_con_nombre_de_quien_los_puso()
    {
        using var ctx = new Context();

        ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "bloquear", new Dictionary<string, string>
        {
            ["resource"] = "consultorio_1",
            ["start"] = "2026-11-16T10:00:00-05:00",
            ["end"] = "2026-11-16T11:00:00-05:00",
            ["reason"] = "Reunión de equipo"
        });

        var day = ctx.Desk.Day(new DateOnly(2026, 11, 16));
        var blockView = Assert.Single(day.Blocks);
        Assert.Equal("Reunión de equipo", blockView.Block.Reason);
        Assert.Equal("Recepcionista", blockView.CreatedByName);
    }
}
