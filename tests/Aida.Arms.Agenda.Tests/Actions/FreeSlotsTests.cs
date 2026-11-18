using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;
using Aida.Engine.Ontology;
using Aida.Platform.Keys;
using Aida.Platform.Packs;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Actions;

public sealed class FreeSlotsTests
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

    private sealed class SlotsSession : IDisposable
    {
        public AgendaWork Work { get; }

        public AgendaTool Tool { get; }

        public ManualTime Time { get; }

        public SlotsSession(AgendaWork work, AgendaTool tool, ManualTime time)
        {
            Work = work;
            Tool = tool;
            Time = time;
        }

        public void Dispose() => Work.Dispose();
    }

    private sealed class Context : IDisposable
    {
        public string DirectoryPath { get; }

        public PlatformDatabase Database { get; }

        public AgendaSetup Setup { get; }

        public MachineKey Key { get; }

        public Sealer Sealer { get; }

        public byte[] PhoneKey { get; }

        public Context()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "aida-slots-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(DirectoryPath);
            Database = new PlatformDatabase(Path.Combine(DirectoryPath, "aida.db"));
            Database.ApplyArmSchema(AgendaDeclarations.ArmId, AgendaSchema.Steps);

            Key = MachineKey.OpenOrCreate(Path.Combine(DirectoryPath, "machine.key"), new DevelopmentKeyProtector());
            Sealer = new Sealer(Key);
            PhoneKey = Key.Derive(AgendaStore.PhoneKeyPurpose);

            var repoRoot = FindRepoRoot();
            var consultorioPath = Path.Combine(repoRoot, "packs", "agenda", "consultorio", "agenda.yaml");
            Setup = AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(consultorioPath), consultorioPath);
        }

        public SlotsSession Begin(DateTimeOffset now)
        {
            var time = new ManualTime();
            time.Now = now;
            var work = AgendaWork.Begin(Database, Sealer, PhoneKey);
            var tool = new AgendaTool(work.Store, Setup, time);
            return new SlotsSession(work, tool, time);
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
    public void Solo_lo_que_agendar_aceptaria()
    {
        using var ctx = new Context();
        var day = new DateOnly(2026, 11, 16);
        var now = DateTimeOffset.Parse("2026-11-16T07:30:00-05:00");

        List<FreeSlot> slots;
        using (var session = ctx.Begin(now))
        {
            slots = FreeSlots.For(session.Work.Store, ctx.Setup, day, "control", now).ToList();
            Assert.NotEmpty(slots);
        }

        // Para cada hueco devuelto, agendar ahí en una transacción nueva debe dar Done
        foreach (var slot in slots.Take(5)) // Probamos los primeros 5
        {
            using var session = ctx.Begin(now);
            var r = session.Tool.Execute(new ActionId("agendar"), new Dictionary<string, string>
            {
                ["name"] = "Paciente",
                ["phone"] = "111",
                ["service"] = "control",
                ["resource"] = slot.Resource,
                ["start"] = StartArgument.Format(slot.Start),
                ["by"] = "u1",
            });
            Assert.Equal(ToolStatus.Done, r.Status);
        }
    }

    [Fact]
    public void Nada_antes_de_ahora()
    {
        using var ctx = new Context();
        var day = new DateOnly(2026, 11, 16);
        var now = DateTimeOffset.Parse("2026-11-16T10:15:00-05:00");

        using var session = ctx.Begin(now);
        var slots = FreeSlots.For(session.Work.Store, ctx.Setup, day, "control", now);
        Assert.NotEmpty(slots);

        foreach (var slot in slots)
        {
            Assert.True(slot.Start >= now, $"El hueco {slot.Start} es anterior a la hora actual {now}");
        }
    }

    [Fact]
    public void Ordenados_por_inicio_y_recurso()
    {
        using var ctx = new Context();
        var day = new DateOnly(2026, 11, 16);
        var now = DateTimeOffset.Parse("2026-11-16T07:30:00-05:00");

        using var session = ctx.Begin(now);
        var slots = FreeSlots.For(session.Work.Store, ctx.Setup, day, "control", now);
        Assert.NotEmpty(slots);

        for (var i = 1; i < slots.Count; i++)
        {
            var prev = slots[i - 1];
            var curr = slots[i];

            Assert.True(
                curr.Start > prev.Start || (curr.Start == prev.Start && string.CompareOrdinal(curr.Resource, prev.Resource) >= 0),
                $"Orden incorrecto: {prev.Start} {prev.Resource} antes de {curr.Start} {curr.Resource}");
        }
    }
}
