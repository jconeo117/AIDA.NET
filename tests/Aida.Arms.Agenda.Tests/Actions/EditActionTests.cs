using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;
using Aida.Platform.Keys;
using Aida.Platform.Packs;
using Aida.Platform.People;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Actions;

public sealed class EditActionTests : IDisposable
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

    private readonly string _dir;
    private readonly PlatformDatabase _db;
    private readonly MachineKey _key;
    private readonly Sealer _sealer;
    private readonly PersonKeys _personKeys;
    private readonly AgendaSetup _setup;

    public EditActionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "aida-edit-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        var dbPath = Path.Combine(_dir, "aida.db");
        _db = new PlatformDatabase(dbPath);

        var keyPath = Path.Combine(_dir, "machine.key");
        _key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
        _sealer = new Sealer(_key);
        _personKeys = PersonKeys.From(_key);
        _db.ApplyArmSchema(AgendaDeclarations.ArmId, AgendaSchema.Steps(_sealer, _personKeys));

        var repoRoot = FindRepoRoot();
        var consultorioPath = Path.Combine(repoRoot, "packs", "agenda", "consultorio", "agenda.yaml");
        _setup = AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(consultorioPath), consultorioPath);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }
        catch
        {
        }
    }

    private AgendaWork Begin() => AgendaWork.Begin(_db, _sealer, _personKeys);

    [Fact]
    public void Reglas_en_su_orden_un_caso_por_motivo()
    {
        using var work = Begin();
        var client1 = work.People.Add("Ana", "3001111111");
        var client2 = work.People.Add("Bob", "3002222222");

        var t8 = DateTimeOffset.Parse("2026-11-16T08:00:00-05:00");
        var app1 = work.Store.Add(client1.Id, "control", "consultorio_1", t8, 30, "u1");
        var app2 = work.Store.Add(client2.Id, "control", "consultorio_1", t8.AddMinutes(30), 30, "u1");

        // 1. Cita inexistente -> unknown_appointment
        var rUnknownApp = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = "999",
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rUnknownApp.Status);
        Assert.Equal("unknown_appointment", rUnknownApp.Reason);

        // 2. Cita cancelada -> not_booked
        work.Store.Update(app2.Id, "u1", state: AppointmentState.Cancelled);
        var rNotBooked = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = app2.Id.ToString(),
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rNotBooked.Status);
        Assert.Equal("not_booked", rNotBooked.Reason);

        // 3. Nombre vacío o solo espacios -> missing_client
        var rEmptyName = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = app1.Id.ToString(),
            ["name"] = "   ",
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rEmptyName.Status);
        Assert.Equal("missing_client", rEmptyName.Reason);

        // 4. Teléfono vacío -> missing_client
        var rEmptyPhone = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = app1.Id.ToString(),
            ["phone"] = "",
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rEmptyPhone.Status);
        Assert.Equal("missing_client", rEmptyPhone.Reason);

        // 5. Servicio desconocido -> unknown_service
        var rUnknownService = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = app1.Id.ToString(),
            ["service"] = "fantasma",
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rUnknownService.Status);
        Assert.Equal("unknown_service", rUnknownService.Reason);

        // 6. Recurso desconocido -> unknown_resource
        var rUnknownRes = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = app1.Id.ToString(),
            ["resource"] = "nadie",
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rUnknownRes.Status);
        Assert.Equal("unknown_resource", rUnknownRes.Reason);

        // 7. Servicio no ofrecido por el recurso (consultorio_2 no ofrece primera_vez) -> service_not_offered
        var rNotOffered = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = app1.Id.ToString(),
            ["resource"] = "consultorio_2",
            ["service"] = "primera_vez",
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rNotOffered.Status);
        Assert.Equal("service_not_offered", rNotOffered.Reason);

        // 8. SlotRules.Rejection al cambiar minutos (procedimiento = 60m choca con app2 a las 08:30)
        work.Store.Update(app2.Id, "u1", state: AppointmentState.Booked);
        var rOverlap = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = app1.Id.ToString(),
            ["service"] = "procedimiento",
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rOverlap.Status);
        Assert.Equal("overlap", rOverlap.Reason);

        // 9. Teléfono compartido con otro contacto -> se admite (N015, D5)
        var rPhoneTaken = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = app1.Id.ToString(),
            ["phone"] = "300 222 2222",
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Done, rPhoneTaken.Status);
    }

    [Fact]
    public void Primer_fallo_gana_en_orden_de_prioridad()
    {
        using var work = Begin();
        var client = work.People.Add("Ana", "3001111111");
        var app = work.Store.Add(client.Id, "control", "consultorio_1", DateTimeOffset.Parse("2026-11-16T08:00:00-05:00"), 30, "u1");

        // missing_client vs unknown_service: missing_client debe ganar
        var r = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = app.Id.ToString(),
            ["name"] = "",
            ["service"] = "invalido",
            ["by"] = "u1"
        });
        Assert.Equal("missing_client", r.Reason);

        // unknown_service vs unknown_resource: unknown_service debe ganar
        var r2 = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = app.Id.ToString(),
            ["service"] = "invalido",
            ["resource"] = "nadie",
            ["by"] = "u1"
        });
        Assert.Equal("unknown_service", r2.Reason);
    }

    [Fact]
    public void Cambio_de_nombre_sin_repasar_hueco_edita_en_tramo_bloqueado()
    {
        using var work = Begin();
        var client = work.People.Add("Ana", "3001111111");
        var t8 = DateTimeOffset.Parse("2026-11-16T08:00:00-05:00");
        var app = work.Store.Add(client.Id, "control", "consultorio_1", t8, 30, "u1");

        // Ahora se bloquea el tramo
        work.Store.AddBlock("consultorio_1", t8, t8.AddHours(1), "Cierre", "admin");

        // Editar solo el nombre no repasa el hueco: se ejecuta con éxito
        var ok = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = app.Id.ToString(),
            ["name"] = "Ana María",
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Done, ok.Status);

        var updatedClient = work.People.Find(client.Id);
        Assert.NotNull(updatedClient);
        Assert.Equal("Ana María", updatedClient.Name);

        // Pero si intenta cambiar el recurso a consultorio_2, repasa el hueco y falla si consultorio_2 estuviera bloqueado
        work.Store.AddBlock("consultorio_2", t8, t8.AddHours(1), "Cierre R2", "admin");
        var fail = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = app.Id.ToString(),
            ["resource"] = "consultorio_2",
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, fail.Status);
        Assert.Equal("outside_hours", fail.Reason); // consultorio_2 abre a las 09:00 los lunes
    }

    [Fact]
    public void Telefono_propio_normalizado_no_es_phone_taken()
    {
        using var work = Begin();
        var client = work.People.Add("Ana", "300 555 0142");
        var app = work.Store.Add(client.Id, "control", "consultorio_1", DateTimeOffset.Parse("2026-11-16T08:00:00-05:00"), 30, "u1");

        // Editar usando el mismo número sin espacios: debe aceptarse porque es su propio teléfono
        var ok = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = app.Id.ToString(),
            ["phone"] = "3005550142",
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Done, ok.Status);

        var c = work.People.Find(client.Id);
        Assert.NotNull(c);
        Assert.Equal("3005550142", c.Phone);
    }

    [Fact]
    public void Cambio_de_telefono_mueve_huella()
    {
        using var work = Begin();
        var client = work.People.Add("Carlos", "3001112233");
        var app = work.Store.Add(client.Id, "control", "consultorio_1", DateTimeOffset.Parse("2026-11-16T08:00:00-05:00"), 30, "u1");

        var ok = EditAction.Run(work.Store, work.People, _setup, new Dictionary<string, string>
        {
            ["appointment"] = app.Id.ToString(),
            ["phone"] = "3009998877",
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Done, ok.Status);

        // ByPhone encuentra con el nuevo y NO con el viejo
        Assert.Empty(work.People.ByPhone("3001112233"));
        var found = work.People.ByPhone("3009998877");
        Assert.NotEmpty(found);
        Assert.Equal(client.Id, found[0].Id);
    }
}
