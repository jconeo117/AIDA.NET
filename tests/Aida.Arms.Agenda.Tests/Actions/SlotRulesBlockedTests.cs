using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Platform.Keys;
using Aida.Platform.Packs;
using Aida.Platform.People;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Actions;

public sealed class SlotRulesBlockedTests : IDisposable
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
    private readonly ResourceSpec _resource;

    public SlotRulesBlockedTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "aida-slotrules-" + Guid.NewGuid().ToString("n"));
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
        _resource = _setup.Resource("consultorio_1")!;
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
    public void Fuera_de_horario_gana_a_bloqueado()
    {
        using var work = Begin();
        // Bloqueo que cubre de 06:00 a 09:00 (abarca fuera de horario y dentro de horario)
        var t6 = DateTimeOffset.Parse("2026-11-16T06:00:00-05:00");
        var t9 = DateTimeOffset.Parse("2026-11-16T09:00:00-05:00");
        work.Store.AddBlock("consultorio_1", t6, t9, "Madrugada", "u1");

        // Intentar a las 07:00 (fuera de horario: consultorio_1 abre a las 08:00)
        var t7 = DateTimeOffset.Parse("2026-11-16T07:00:00-05:00");
        var rejection = SlotRules.Rejection(work.Store, _resource, t7, 30, except: null);

        Assert.Equal("outside_hours", rejection);
    }

    [Fact]
    public void Bloqueado_gana_a_solape()
    {
        using var work = Begin();
        var t8 = DateTimeOffset.Parse("2026-11-16T08:00:00-05:00");
        var client = work.People.Add("Paciente", "3001112233");

        // Hay una cita agendada a las 08:00
        work.Store.Add(client.Id, "control", "consultorio_1", t8, 30, "u1");

        // Y también hay un bloqueo a las 08:00
        work.Store.AddBlock("consultorio_1", t8, t8.AddHours(1), "Cierre", "admin");

        // Intentar agendar a las 08:00: coincide con cita Y bloqueo -> bloqueado gana a solape
        var rejection = SlotRules.Rejection(work.Store, _resource, t8, 30, except: null);
        Assert.Equal("blocked", rejection);
    }

    [Fact]
    public void Bloqueo_que_acaba_en_el_inicio_no_bloquea()
    {
        using var work = Begin();
        var t8 = DateTimeOffset.Parse("2026-11-16T08:00:00-05:00");
        var t9 = DateTimeOffset.Parse("2026-11-16T09:00:00-05:00");

        // Bloqueo de 08:00 a 09:00
        work.Store.AddBlock("consultorio_1", t8, t9, "Reunión", "u1");

        // Hueco a las 09:00: el bloqueo acaba justo a las 09:00 -> no bloquea
        var rejection = SlotRules.Rejection(work.Store, _resource, t9, 30, except: null);
        Assert.Null(rejection);
    }

    [Fact]
    public void FreeSlots_For_no_ofrece_huecos_bloqueados()
    {
        using var work = Begin();
        var day = new DateOnly(2026, 11, 16); // Lunes
        var now = DateTimeOffset.Parse("2026-11-16T07:00:00-05:00");

        // Huecos libres iniciales
        var initialSlots = FreeSlots.For(work.Store, _setup, day, "control", now);
        var initialR1Slots = initialSlots.Where(s => s.Resource == "consultorio_1").ToList();
        // Horario consultorio_1: 08:00 a 13:00 (10 huecos de 30m: 08:00, 08:30, 09:00, 09:30, 10:00, 10:30, 11:00, 11:30, 12:00, 12:30)
        Assert.Equal(10, initialR1Slots.Count);

        // Bloqueo de consultorio_1 de 09:00 a 10:30 (excluye 09:00, 09:30, 10:00 = 3 huecos)
        var t9 = DateTimeOffset.Parse("2026-11-16T09:00:00-05:00");
        var t1030 = DateTimeOffset.Parse("2026-11-16T10:30:00-05:00");
        work.Store.AddBlock("consultorio_1", t9, t1030, "Mantenimiento", "admin");

        var slotsAfterBlock = FreeSlots.For(work.Store, _setup, day, "control", now);
        var r1SlotsAfter = slotsAfterBlock.Where(s => s.Resource == "consultorio_1").ToList();
        Assert.Equal(7, r1SlotsAfter.Count);

        var startTimes = r1SlotsAfter.Select(s => s.Start.ToString("HH:mm")).ToList();
        Assert.DoesNotContain("09:00", startTimes);
        Assert.DoesNotContain("09:30", startTimes);
        Assert.DoesNotContain("10:00", startTimes);
        Assert.Contains("08:00", startTimes);
        Assert.Contains("08:30", startTimes);
        Assert.Contains("10:30", startTimes);
    }
}
