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
                });
            Setup = new AgendaSetup(
                baseSetup.Sector,
                baseSetup.SlotMinutes,
                baseSetup.ClientLabel,
                baseSetup.Services,
                [.. baseSetup.Resources, tardeResource]);

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

            // Contactos iniciales según contrato: Ana Pérez, Ana Gómez y Juan Ruiz
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

    [Fact]
    public void Valida_las_nueve_frases_del_contrato_segun_especificacion()
    {
        using var ctx = new Context();
        var today = DateOnly.FromDateTime(ctx.Time.Now.DateTime);

        // 1. cita para Juan Ruiz mañana a las 10, control -> NewAppointment, 11-25, control, 10:00, TimeTaken = false
        var p1 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Juan Ruiz mañana a las 10, control", today));
        Assert.Equal(ProposalKind.NewAppointment, p1.Kind);
        Assert.Equal(new DateOnly(2026, 11, 25), p1.Day);
        Assert.Equal("control", p1.Service);
        Assert.Equal(new TimeOnly(10, 0), p1.Time);
        Assert.False(p1.TimeTaken);
        Assert.Equal("Juan Ruiz", p1.Prefill?.Name);

        // 2. cita para Juan hoy a las 3 -> 15:00 (D12: las 03:00 caen fuera y las 15:00 dentro)
        var p2 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Juan hoy a las 3", today));
        Assert.Equal(ProposalKind.NewAppointment, p2.Kind);
        Assert.Equal(today, p2.Day);
        Assert.Equal(new TimeOnly(15, 0), p2.Time);
        Assert.False(p2.TimeTaken);

        // 3. cita para Juan hoy a las 10 -> 10:00 (cae dentro por la mañana)
        var p3 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Juan hoy a las 10", today));
        Assert.Equal(ProposalKind.NewAppointment, p3.Kind);
        Assert.Equal(today, p3.Day);
        Assert.Equal(new TimeOnly(10, 0), p3.Time);
        Assert.False(p3.TimeTaken);

        // 4. cita para Juan hoy a las 10 con la de las 10:00 ocupada -> 10:00, TimeTaken = true
        ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "agendar", new Dictionary<string, string>
        {
            ["name"] = "Paciente Ocupado",
            ["phone"] = "3009998877",
            ["service"] = "primera_vez",
            ["start"] = "2026-11-24T10:00:00-05:00",
        });
        var p4 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Juan hoy a las 10", today));
        Assert.Equal(ProposalKind.NewAppointment, p4.Kind);
        Assert.Equal(today, p4.Day);
        Assert.Equal(new TimeOnly(10, 0), p4.Time);
        Assert.True(p4.TimeTaken);

        // 5. qué hay libre el jueves en la tarde -> FreeSlots, 11-26, primer servicio, 14:00 (D18)
        // Se bloquea el tramo 12:00-13:00 de consultorio_1 para que la tarde libre comience en el recurso de tarde (14:00)
        ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "bloquear", new Dictionary<string, string>
        {
            ["start"] = "2026-11-26T12:00:00-05:00",
            ["end"] = "2026-11-26T13:00:00-05:00",
            ["resource"] = "consultorio_1",
            ["reason"] = "almuerzo",
        });
        var p5 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("qué hay libre el jueves en la tarde", today));
        Assert.Equal(ProposalKind.FreeSlots, p5.Kind);
        Assert.Equal(new DateOnly(2026, 11, 26), p5.Day);
        Assert.Equal("primera_vez", p5.Service);
        Assert.Equal(new TimeOnly(14, 0), p5.Time);
        Assert.False(p5.TimeTaken);

        // 6. qué hay libre el jueves en la tarde, con la tarde llena -> TimeTaken = true, Time = null
        ctx.Desk.Submit(ctx.Reception, "127.0.0.1", "bloquear", new Dictionary<string, string>
        {
            ["start"] = "2026-11-26T14:00:00-05:00",
            ["end"] = "2026-11-26T18:00:00-05:00",
            ["resource"] = "tarde",
            ["reason"] = "cerrado",
        });
        var p6 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("qué hay libre el jueves en la tarde", today));
        Assert.Equal(ProposalKind.FreeSlots, p6.Kind);
        Assert.Equal(new DateOnly(2026, 11, 26), p6.Day);
        Assert.Null(p6.Time);
        Assert.True(p6.TimeTaken);

        // 7. cita para Juan el 20/11 -> PastDay, 11-20
        var p7 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Juan el 20/11", today));
        Assert.Equal(ProposalKind.PastDay, p7.Kind);
        Assert.Equal(new DateOnly(2026, 11, 20), p7.Day);
        Assert.Null(p7.Time);
        Assert.False(p7.TimeTaken);

        // 8. cita para Juan -> hoy, el primer servicio, Time = null
        var p8 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Juan", today));
        Assert.Equal(ProposalKind.NewAppointment, p8.Kind);
        Assert.Equal(today, p8.Day);
        Assert.Equal("primera_vez", p8.Service);
        Assert.Null(p8.Time);
        Assert.False(p8.TimeTaken);

        // 9. cancela la cita de Juan mañana -> OpenCard (N013 no lee fechas para cancelar)
        var p9 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cancela la cita de Juan mañana", today));
        Assert.Equal(ProposalKind.OpenCard, p9.Kind);
        Assert.Single(p9.Clients);
        Assert.Equal("Juan Ruiz", p9.Clients[0].Name);
        Assert.Null(p9.Time);
        Assert.False(p9.TimeTaken);
    }

    [Fact]
    public void Valida_intenciones_y_busqueda_de_contactos_del_asistente()
    {
        using var ctx = new Context();
        var today = DateOnly.FromDateTime(ctx.Time.Now.DateTime);

        // cita para Carlos Díaz -> NewAppointment, Prefill = (0, Carlos Díaz, "")
        var p1 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Carlos Díaz", today));
        Assert.Equal(ProposalKind.NewAppointment, p1.Kind);
        Assert.NotNull(p1.Prefill);
        Assert.Equal(0, p1.Prefill.Id);
        Assert.Equal("Carlos Díaz", p1.Prefill.Name);
        Assert.Equal(string.Empty, p1.Prefill.Phone);

        // agéndame al 3005551234 -> NewAppointment con Juan
        var p2 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("agéndame al 3005551234", today));
        Assert.Equal(ProposalKind.NewAppointment, p2.Kind);
        Assert.NotNull(p2.Prefill);
        Assert.Equal("Juan Ruiz", p2.Prefill.Name);

        // cita para Ana -> PickClientForNew: Ana Gómez y Ana Pérez
        var p3 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cita para Ana", today));
        Assert.Equal(ProposalKind.PickClientForNew, p3.Kind);
        Assert.Equal(2, p3.Clients.Count);
        Assert.Equal("Ana Gómez", p3.Clients[0].Name);
        Assert.Equal("Ana Pérez", p3.Clients[1].Name);

        // mueve la cita de Ana -> PickClientForCard, las dos
        var p4 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("mueve la cita de Ana", today));
        Assert.Equal(ProposalKind.PickClientForCard, p4.Kind);
        Assert.Equal(2, p4.Clients.Count);

        // cancela la cita de Pedro -> NoClient
        var p5 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("cancela la cita de Pedro", today));
        Assert.Equal(ProposalKind.NoClient, p5.Kind);
        Assert.Empty(p5.Clients);

        // buenos días -> NotUnderstood
        var p6 = ctx.Assistant.Propose(ctx.Interpreter.Interpret("buenos días", today));
        Assert.Equal(ProposalKind.NotUnderstood, p6.Kind);
    }
}
