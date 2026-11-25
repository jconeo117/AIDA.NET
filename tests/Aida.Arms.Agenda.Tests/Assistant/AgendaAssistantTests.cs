using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Assistant;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Cognition.Understanding;
using Aida.Engine.Actions;
using Aida.Engine.Ontology;
using Aida.Platform.Assistant;
using Aida.Platform.Audit;
using Aida.Platform.Identity;
using Aida.Platform.Keys;
using Aida.Platform.Packs;
using Aida.Platform.Roles;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Assistant;

public sealed class AgendaAssistantTests
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
        public AssistantInterpreter Interpreter { get; }
        public AgendaAssistant Assistant { get; }

        public Context()
        {
            var repoRoot = FindRepoRoot();
            DirectoryPath = Path.Combine(Path.GetTempPath(), "aida-asistente-test-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(DirectoryPath);

            Db = new TestDatabase();
            Time = new ManualTime();
            Time.Now = DateTimeOffset.Parse("2026-11-24T07:30:00-05:00");

            Audit = new AuditLog(Db.Database, Time, AgendaDeclarations.AuditKinds);

            var rolesPath = Path.Combine(repoRoot, "packs", "platform", "roles.yaml");
            Roles = RoleCatalog.Load(rolesPath, AgendaDeclarations.Permissions);

            var hasher = new PasswordHasher(1);
            Users = new UserStore(Db.Database, Audit, Roles, hasher, Time);

            Admin = Users.CreateFirstAdministrator("admin", "Administradora", "PassAdmin123!", "127.0.0.1");
            Reception = Users.Create(Admin.Id, "127.0.0.1", "recep", "Recepcionista", "recepcion", "PassRecep123!");

            var consultorioPath = Path.Combine(repoRoot, "packs", "agenda", "consultorio", "agenda.yaml");
            Setup = AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(consultorioPath), consultorioPath);

            var armPath = Path.Combine(repoRoot, "packs", "agenda", "arm.yaml");
            Catalog = AgendaManifest.Load(armPath).Actions;

            var keyPath = Path.Combine(DirectoryPath, "machine.key");
            Key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());

            Desk = new AgendaDesk(Db.Database, Audit, Roles, Users, Setup, Catalog, Reversibility.Reversible, Key, Time);

            var catalogPath = Path.Combine(repoRoot, "packs", "agenda", "intents.yaml");
            var intentCatalog = IntentCatalog.Load(catalogPath, AgendaDeclarations.Intents);
            var timePath = Path.Combine(repoRoot, "packs", "platform", "time.yaml");
            var timeVocab = TimeVocabulary.Load(timePath);

            Interpreter = new AssistantInterpreter(intentCatalog, timeVocab);
            Assistant = new AgendaAssistant(Desk, Time);

            // Contactos iniciales según contrato §B.2: Ana Pérez, Ana Gómez y Juan Ruiz (tel 3005551234)
            Desk.Submit(Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
            {
                ["name"] = "Ana Pérez",
                ["phone"] = "3001111111",
                ["service"] = "control",
                ["start"] = "2026-11-24T08:00:00-05:00",
            });
            Desk.Submit(Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
            {
                ["name"] = "Ana Gómez",
                ["phone"] = "3002222222",
                ["service"] = "control",
                ["start"] = "2026-11-24T08:30:00-05:00",
            });
            Desk.Submit(Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
            {
                ["name"] = "Juan Ruiz",
                ["phone"] = "3005551234",
                ["service"] = "control",
                ["start"] = "2026-11-24T09:00:00-05:00",
            });
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
    public void Valida_las_diez_frases_del_contrato_en_cadena_real()
    {
        using var ctx = new Context();
        var today = DateOnly.FromDateTime(ctx.Time.Now.DateTime);

        // 1. "cita para Juan Ruiz, control" -> NewAppointment, control, Juan Ruiz
        var p1 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Juan Ruiz, control"));
        Assert.Equal(ProposalKind.NewAppointment, p1.Kind);
        Assert.Equal(today, p1.Day);
        Assert.Equal("control", p1.Service);
        Assert.NotNull(p1.Prefill);
        Assert.Equal("Juan Ruiz", p1.Prefill.Name);
        Assert.False(p1.MentionsTime);

        // 2. "cita para Carlos Díaz" -> NewAppointment, Prefill = (0, Carlos Díaz, "")
        var p2 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Carlos Díaz"));
        Assert.Equal(ProposalKind.NewAppointment, p2.Kind);
        Assert.NotNull(p2.Prefill);
        Assert.Equal(0, p2.Prefill.Id);
        Assert.Equal("Carlos Díaz", p2.Prefill.Name);
        Assert.Equal(string.Empty, p2.Prefill.Phone);
        Assert.False(p2.MentionsTime);

        // 3. "agéndame al 3005551234" (Juan tiene ese teléfono) -> NewAppointment con Juan
        var p3 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("agéndame al 3005551234"));
        Assert.Equal(ProposalKind.NewAppointment, p3.Kind);
        Assert.NotNull(p3.Prefill);
        Assert.Equal("Juan Ruiz", p3.Prefill.Name);
        Assert.False(p3.MentionsTime);

        // 4. "cita para Ana" -> PickClientForNew: Ana Gómez y Ana Pérez en ese orden
        var p4 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Ana"));
        Assert.Equal(ProposalKind.PickClientForNew, p4.Kind);
        Assert.Null(p4.Prefill);
        Assert.Equal(2, p4.Clients.Count);
        Assert.Equal("Ana Gómez", p4.Clients[0].Name);
        Assert.Equal("Ana Pérez", p4.Clients[1].Name);
        Assert.False(p4.MentionsTime);

        // 5. "qué hay libre para procedimiento" -> FreeSlots, procedimiento
        var p5 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("qué hay libre para procedimiento"));
        Assert.Equal(ProposalKind.FreeSlots, p5.Kind);
        Assert.Equal("procedimiento", p5.Service);
        Assert.Null(p5.Prefill);
        Assert.Empty(p5.Clients);
        Assert.False(p5.MentionsTime);

        // 6. "cancela la cita de Juan" -> OpenCard con Juan
        var p6 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cancela la cita de Juan"));
        Assert.Equal(ProposalKind.OpenCard, p6.Kind);
        Assert.Single(p6.Clients);
        Assert.Equal("Juan Ruiz", p6.Clients[0].Name);
        Assert.False(p6.MentionsTime);

        // 7. "mueve la cita de Ana" -> PickClientForCard, las dos
        var p7 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("mueve la cita de Ana"));
        Assert.Equal(ProposalKind.PickClientForCard, p7.Kind);
        Assert.Equal(2, p7.Clients.Count);
        Assert.Equal("Ana Gómez", p7.Clients[0].Name);
        Assert.Equal("Ana Pérez", p7.Clients[1].Name);
        Assert.False(p7.MentionsTime);

        // 8. "cancela la cita de Pedro" -> NoClient
        var p8 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cancela la cita de Pedro"));
        Assert.Equal(ProposalKind.NoClient, p8.Kind);
        Assert.Empty(p8.Clients);
        Assert.False(p8.MentionsTime);

        // 9. "cita para Juan mañana a las 10" -> NewAppointment con Juan, MentionsTime = true
        var p9 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Juan mañana a las 10"));
        Assert.Equal(ProposalKind.NewAppointment, p9.Kind);
        Assert.NotNull(p9.Prefill);
        Assert.Equal("Juan Ruiz", p9.Prefill.Name);
        Assert.True(p9.MentionsTime);

        // 10. "buenos días" -> NotUnderstood
        var p10 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("buenos días"));
        Assert.Equal(ProposalKind.NotUnderstood, p10.Kind);
        Assert.Null(p10.Service);
        Assert.Null(p10.Prefill);
        Assert.Empty(p10.Clients);
        Assert.False(p10.MentionsTime);
    }
}
