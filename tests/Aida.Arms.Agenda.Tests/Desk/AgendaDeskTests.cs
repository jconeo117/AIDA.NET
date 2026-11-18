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

public sealed class AgendaDeskTests
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

        public Context(bool withAgendaAuditKind = true)
        {
            var repoRoot = FindRepoRoot();
            DirectoryPath = Path.Combine(Path.GetTempPath(), "aida-desk-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(DirectoryPath);

            Db = new TestDatabase();
            Time = new ManualTime();
            Time.Now = DateTimeOffset.Parse("2026-11-16T07:30:00-05:00");

            var kinds = withAgendaAuditKind ? AgendaDeclarations.AuditKinds : [];
            Audit = new AuditLog(Db.Database, Time, kinds);

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
    public void Sin_permiso_es_forbidden_y_no_hay_evento_ni_cambio()
    {
        using var ctx = new Context();

        var result = ctx.Desk.Submit(ctx.Cashier, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Paciente",
            ["phone"] = "111",
            ["service"] = "control",
            ["start"] = "2026-11-16T08:00:00-05:00",
        });

        Assert.Equal(ToolStatus.Rejected, result.Status);
        Assert.Equal("forbidden", result.Reason);

        // No hay eventos de la puerta
        Assert.Empty(ctx.Desk.EventLines());

        // No hay citas en el día
        var day = ctx.Desk.Day(new DateOnly(2026, 11, 16));
        Assert.Empty(day.Appointments);
    }

    [Fact]
    public void Done_escribe_la_cita_y_agenda_changed_en_la_misma_transaccion()
    {
        using var ctx = new Context();
        const string workstation = "192.168.1.50";
        const string startStr = "2026-11-16T08:00:00-05:00";

        var result = ctx.Desk.Submit(ctx.Reception, workstation, "agendar", new Dictionary<string, string>
        {
            ["name"] = "Ana Pérez",
            ["phone"] = "3005550142",
            ["service"] = "control",
            ["start"] = startStr,
        });

        Assert.Equal(ToolStatus.Done, result.Status);
        var appId = result.Data["appointment"];

        // Comprobamos la cita en el día
        var day = ctx.Desk.Day(new DateOnly(2026, 11, 16));
        Assert.Single(day.Appointments);
        var app = day.Appointments[0];
        Assert.Equal(appId, app.Appointment.Id.ToString());
        Assert.Equal("Ana Pérez", app.ClientName);

        // Comprobamos la línea de auditoría
        var lines = ctx.Audit.Lines();
        var lastLine = lines[^1];
        using var doc = System.Text.Json.JsonDocument.Parse(lastLine);
        var root = doc.RootElement;
        Assert.Equal("agenda_changed", root.GetProperty("kind").GetString());
        Assert.Equal(ctx.Reception.Actor.Id, root.GetProperty("actor").GetString());
        Assert.Equal(workstation, root.GetProperty("workstation").GetString());

        var dataJson = root.GetProperty("data").GetString()!;
        using var dataDoc = System.Text.Json.JsonDocument.Parse(dataJson);
        var dataRoot = dataDoc.RootElement;
        Assert.Equal("agendar", dataRoot.GetProperty("action").GetString());
        Assert.Equal(appId, dataRoot.GetProperty("appointment").GetString());
        Assert.Equal(startStr, dataRoot.GetProperty("start").GetString());

        Assert.Empty(ctx.Audit.Verify());
    }

    [Fact]
    public void Un_rechazo_no_escribe_auditoria()
    {
        using var ctx = new Context();
        var auditLinesBefore = ctx.Audit.Lines().Count;

        // Petición que se rechaza por past
        var result = ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "111",
            ["service"] = "control",
            ["start"] = "2026-11-16T07:00:00-05:00", // Pasado (reloj está a las 07:30)
        });

        Assert.Equal(ToolStatus.Rejected, result.Status);
        Assert.Equal("past", result.Reason);

        var auditLinesAfter = ctx.Audit.Lines().Count;
        Assert.Equal(auditLinesBefore, auditLinesAfter);
    }

    [Fact]
    public void Si_la_auditoria_falla_la_cita_no_queda()
    {
        // Creamos contexto SIN la clase agenda_changed declarada en AuditLog
        using var ctx = new Context(withAgendaAuditKind: false);

        // Al enviar, ActionGate emitirá y ejecutará la acción, pero al intentar _audit.Record lanzará ArgumentException
        Assert.Throws<ArgumentException>(() =>
            ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
            {
                ["name"] = "Ana",
                ["phone"] = "111",
                ["service"] = "control",
                ["start"] = "2026-11-16T08:00:00-05:00",
            }));

        // La cita NO debe haber quedado confirmada en la base
        var day = ctx.Desk.Day(new DateOnly(2026, 11, 16));
        Assert.Empty(day.Appointments);
    }

    [Fact]
    public void By_lo_pone_la_sesion()
    {
        using var ctx = new Context();

        // Si quien llama pasa un 'by' malicioso, Desk lo sobrescribe con el id del usuario de la sesión
        var result = ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "111",
            ["service"] = "control",
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["by"] = "hacker",
        });

        Assert.Equal(ToolStatus.Done, result.Status);
        var appId = long.Parse(result.Data["appointment"]);

        var day = ctx.Desk.Day(new DateOnly(2026, 11, 16));
        var app = day.Appointments.First(a => a.Appointment.Id == appId);
        Assert.Equal(ctx.Reception.Actor.Id, app.Appointment.CreatedBy);
    }

    [Fact]
    public void Day_resuelve_nombres_y_trae_las_canceladas()
    {
        using var ctx = new Context();

        // 1. Agendamos cita
        var rBook = ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Ana",
            ["phone"] = "111",
            ["service"] = "control",
            ["start"] = "2026-11-16T08:00:00-05:00",
        });
        var appId = rBook.Data["appointment"];

        // 2. Cancelamos la cita
        ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "cancelar", new Dictionary<string, string>
        {
            ["appointment"] = appId,
        });

        // 3. Consultamos el día
        var day = ctx.Desk.Day(new DateOnly(2026, 11, 16));
        Assert.Single(day.Appointments);
        var app = day.Appointments[0];

        Assert.Equal(AppointmentState.Cancelled, app.Appointment.State);
        // Resuelve los nombres de visualización a partir de _users
        Assert.Equal("Recepcionista", app.CreatedByName);
        Assert.Equal("Recepcionista", app.UpdatedByName);
    }
}
