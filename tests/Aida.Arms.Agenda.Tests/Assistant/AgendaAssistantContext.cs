using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Assistant;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Cognition.Interpretation;
using Aida.Engine.Actions;
using Aida.Engine.Ontology;
using Aida.Platform.Assistant;
using Aida.Platform.Audit;
using Aida.Platform.Identity;
using Aida.Platform.Keys;
using Aida.Platform.Packs;
using Aida.Platform.People;
using Aida.Platform.Roles;
using Microsoft.Data.Sqlite;

namespace Aida.Arms.Agenda.Tests.Assistant;

internal sealed class AgendaAssistantContext : IDisposable
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
    public AssistantInterpreter Interpreter { get; }
    public AgendaAssistant Assistant { get; }

    internal static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    public AgendaAssistantContext()
    {
        var repoRoot = FindRepoRoot();
        DirectoryPath = Path.Combine(Path.GetTempPath(), "aida-asistente-test-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(DirectoryPath);

        Db = new TestDatabase();
        Time = new ManualTime();
        Time.Now = DateTimeOffset.Parse("2026-11-24T07:30:00-05:00");

        Audit = new AuditLog(Db.Database, Time, AgendaDeclarations.AuditKinds);

        var rolesPath = Path.Combine(repoRoot, "packs", "platform", "roles.yaml");
        Roles = RoleCatalog.Load(rolesPath, [.. AgendaDeclarations.Permissions, "health.read", "health.record", "health.sign"]);

        var hasher = new PasswordHasher(1);
        Users = new UserStore(Db.Database, Audit, Roles, hasher, Time);

        Admin = Users.CreateFirstAdministrator("admin", "Administradora", "PassAdmin123!", "127.0.0.1");
        Reception = Users.Create(Admin.Id, "127.0.0.1", "recep", "Recepcionista", "recepcion", "PassRecep123!");

        var consultorioPath = Path.Combine(repoRoot, "packs", "agenda", "consultorio", "agenda.yaml");
        var baseSetup = AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(consultorioPath), consultorioPath);
        var tardeResource = new ResourceSpec(
            "tarde",
            "Profesional Tarde",
            "Consultorio Tarde",
            baseSetup.Services.Select(s => s.Id).ToList(),
            new Dictionary<DayOfWeek, IReadOnlyList<TimeRange>>
            {
                [DayOfWeek.Tuesday] = [new TimeRange(new TimeOnly(14, 0), new TimeOnly(18, 0))],
                [DayOfWeek.Thursday] = [new TimeRange(new TimeOnly(14, 0), new TimeOnly(18, 0))],
                [DayOfWeek.Friday] = [new TimeRange(new TimeOnly(14, 0), new TimeOnly(18, 0))],
            });
        Setup = new AgendaSetup(
            baseSetup.Sector,
            baseSetup.SlotMinutes,
            baseSetup.ClientLabel,
            baseSetup.Services,
            [.. baseSetup.Resources, tardeResource]);

        var armPath = Path.Combine(repoRoot, "packs", "agenda", "arm.yaml");
        Catalog = AgendaManifest.Load(armPath).Actions;

        var people = PersonCatalog.Load(Path.Combine(repoRoot, "packs", "platform", "people.yaml"));

        var keyPath = Path.Combine(DirectoryPath, "machine.key");
        Key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());

        Desk = new AgendaDesk(Db.Database, Audit, Roles, Users, Setup, people, Catalog, Reversibility.Reversible, Key, Time);

        var catalogPath = Path.Combine(repoRoot, "packs", "agenda", "intents.yaml");
        var intentCatalog = IntentCatalog.Load(catalogPath, AgendaDeclarations.Intents);
        var timePath = Path.Combine(repoRoot, "packs", "platform", "time.yaml");
        var timeVocab = TimeVocabulary.Load(timePath);

        Interpreter = new AssistantInterpreter(intentCatalog, timeVocab);
        Assistant = new AgendaAssistant(Desk, Time);

        // Sembrado según especificación §B.3 (N014)
        Desk.Submit(Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Juan Ruiz",
            ["phone"] = "3005551234",
            ["service"] = "control",
            ["start"] = "2026-11-24T08:00:00-05:00",
        });
        Desk.Submit(Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Juan Ruiz",
            ["phone"] = "3005551234",
            ["service"] = "control",
            ["start"] = "2026-11-24T11:00:00-05:00",
        });
        Desk.Submit(Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Juan Ruiz",
            ["phone"] = "3005551234",
            ["service"] = "control",
            ["start"] = "2026-11-26T08:00:00-05:00",
        });
        Desk.Submit(Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Ana Pérez",
            ["phone"] = "3001111111",
            ["service"] = "control",
            ["start"] = "2026-11-25T10:00:00-05:00",
        });
        Desk.Submit(Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Ana Gómez",
            ["phone"] = "3002222222",
            ["service"] = "control",
            ["start"] = "2026-11-27T15:00:00-05:00",
        });

        var pedroRes = Desk.Submit(Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Pedro Díaz",
            ["phone"] = "3003333333",
            ["service"] = "control",
            ["start"] = "2026-11-25T08:00:00-05:00",
        });
        var pedroAppt = pedroRes.Data["appointment"];
        Desk.Submit(Reception, "127.0.0.1", "cancelar", new Dictionary<string, string>
        {
            ["appointment"] = pedroAppt,
        });

        // Reloj fijado a las 09:00 para la ejecución de los tests
        Time.Now = DateTimeOffset.Parse("2026-11-24T09:00:00-05:00");
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
