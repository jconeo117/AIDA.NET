using System.Text.Json;
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
using Aida.Platform.Roles;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Desk;

public sealed class AgendaDeskConsultaTests
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

        public UserAccount Cashier { get; }

        public Context()
        {
            var repoRoot = FindRepoRoot();
            DirectoryPath = Path.Combine(Path.GetTempPath(), "aida-desk-consulta-" + Guid.NewGuid().ToString("n"));
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
            Cashier = Users.Create(Admin.Id, "127.0.0.1", "caja", "Cajero", "caja", "PassCaja123!");

            var consultorioPath = Path.Combine(repoRoot, "packs", "agenda", "consultorio", "agenda.yaml");
            Setup = AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(consultorioPath), consultorioPath);

            var armPath = Path.Combine(repoRoot, "packs", "agenda", "arm.yaml");
            Catalog = AgendaManifest.Load(armPath).Actions;

            var keyPath = Path.Combine(DirectoryPath, "machine.key");
            Key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());

            Desk = new AgendaDesk(Db.Database, Audit, Roles, Users, Setup, Catalog, Reversibility.Reversible, Key, Time);
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
    public void Caja_puede_buscar_y_ver_ficha_pero_editar_contacto_da_forbidden_sin_evento()
    {
        using var ctx = new Context();

        // 1. Recepción agenda una cita para crear el contacto
        var agendarResult = ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Ana Pérez",
            ["phone"] = "3005550142",
            ["service"] = "control",
            ["start"] = "2026-11-16T08:00:00-05:00",
        });
        Assert.Equal(ToolStatus.Done, agendarResult.Status);

        var eventsBefore = ctx.Desk.EventLines().Count;

        // 2. Caja (sin permisos de agenda.manage) busca y encuentra
        var encontrados = ctx.Desk.Search("Ana");
        var client = Assert.Single(encontrados);
        Assert.Equal("Ana Pérez", client.Name);

        // 3. Caja ve la ficha completa
        var card = ctx.Desk.Card(client.Id);
        Assert.NotNull(card);
        Assert.Equal("Ana Pérez", card.Client.Name);
        Assert.Single(card.Appointments);

        // 4. Caja intenta editar contacto: rechazado por falta de permisos
        var editResult = ctx.Desk.Submit(ctx.Cashier, "127.0.0.1", "editar_contacto", new Dictionary<string, string>
        {
            ["client"] = client.Id.ToString(),
            ["name"] = "Ana María Pérez",
            ["phone"] = "3005550142",
        });

        Assert.Equal(ToolStatus.Rejected, editResult.Status);
        Assert.Equal("forbidden", editResult.Reason);

        // Sin evento nuevo en la puerta de acciones
        Assert.Equal(eventsBefore, ctx.Desk.EventLines().Count);
    }

    [Fact]
    public void Recepcion_edita_contacto_y_deja_agenda_changed_con_client_sin_appointment_ni_block()
    {
        using var ctx = new Context();

        var agendarResult = ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Carlos Ruiz",
            ["phone"] = "3101112233",
            ["service"] = "control",
            ["start"] = "2026-11-16T08:00:00-05:00",
        });
        Assert.Equal(ToolStatus.Done, agendarResult.Status);

        var client = ctx.Desk.Search("Carlos")[0];

        // Recepción edita contacto
        var editResult = ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "editar_contacto", new Dictionary<string, string>
        {
            ["client"] = client.Id.ToString(),
            ["name"] = "Carlos Andrés Ruiz",
            ["phone"] = "3101112233",
        });

        Assert.Equal(ToolStatus.Done, editResult.Status);
        Assert.Equal(client.Id.ToString(), editResult.Data["client"]);

        // Comprobamos la línea de auditoría
        var lines = ctx.Audit.Lines();
        var lastLine = lines[^1];
        using var doc = JsonDocument.Parse(lastLine);
        var root = doc.RootElement;
        Assert.Equal("agenda_changed", root.GetProperty("kind").GetString());
        Assert.Equal(ctx.Reception.Actor.Id, root.GetProperty("actor").GetString());

        var dataJson = root.GetProperty("data").GetString()!;
        using var dataDoc = JsonDocument.Parse(dataJson);
        var dataRoot = dataDoc.RootElement;
        Assert.Equal("editar_contacto", dataRoot.GetProperty("action").GetString());
        Assert.Equal(client.Id.ToString(), dataRoot.GetProperty("client").GetString());
        Assert.False(dataRoot.TryGetProperty("appointment", out _));
        Assert.False(dataRoot.TryGetProperty("block", out _));

        Assert.Empty(ctx.Audit.Verify());
    }

    [Fact]
    public void Card_de_contacto_inexistente_devuelve_null()
    {
        using var ctx = new Context();
        var card = ctx.Desk.Card(99999);
        Assert.Null(card);
    }

    [Fact]
    public void Day_trae_la_semana_correcta_un_domingo_y_un_lunes()
    {
        using var ctx = new Context();

        // 1. Consultado en un lunes (2026-11-16)
        var lunes = new DateOnly(2026, 11, 16);
        var vistaLunes = ctx.Desk.Day(lunes);
        Assert.Equal(6, vistaLunes.Week.Count);
        Assert.Equal(lunes, vistaLunes.Week[0].Day);

        // 2. Consultado en un domingo (2026-11-22)
        var domingo = new DateOnly(2026, 11, 22);
        var vistaDomingo = ctx.Desk.Day(domingo);
        Assert.Equal(6, vistaDomingo.Week.Count);
        Assert.Equal(lunes, vistaDomingo.Week[0].Day);
    }

    [Fact]
    public void Buscar_no_deja_auditoria()
    {
        using var ctx = new Context();
        var auditLinesBefore = ctx.Audit.Lines().Count;

        ctx.Desk.Search("Ana");
        ctx.Desk.Search("300");
        ctx.Desk.Search("");

        var auditLinesAfter = ctx.Audit.Lines().Count;
        Assert.Equal(auditLinesBefore, auditLinesAfter);
    }
}
