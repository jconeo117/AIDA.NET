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

public sealed class WeekSummaryTests : IDisposable
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
    private readonly TimeSpan _offset = TimeSpan.FromHours(-5);

    public WeekSummaryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "aida-weeksummary-" + Guid.NewGuid().ToString("n"));
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
        if (Directory.Exists(_dir))
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void Retorna_seis_dias_de_lunes_a_sabado()
    {
        var lunes = new DateOnly(2026, 11, 16);
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        var semana = WeekSummary.For(work.Store, _setup, lunes, _offset);

        Assert.Equal(6, semana.Count);
        Assert.Equal(DayOfWeek.Monday, semana[0].Day.DayOfWeek);
        Assert.Equal(DayOfWeek.Tuesday, semana[1].Day.DayOfWeek);
        Assert.Equal(DayOfWeek.Wednesday, semana[2].Day.DayOfWeek);
        Assert.Equal(DayOfWeek.Thursday, semana[3].Day.DayOfWeek);
        Assert.Equal(DayOfWeek.Friday, semana[4].Day.DayOfWeek);
        Assert.Equal(DayOfWeek.Saturday, semana[5].Day.DayOfWeek);
    }

    [Fact]
    public void Bloqueo_especifico_solo_resta_a_su_recurso_y_bloqueo_en_borde_no_resta()
    {
        var lunes = new DateOnly(2026, 11, 16);
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);

        // Bloqueo de 08:00 a 09:00 en consultorio_1 (2 huecos de 30 min)
        var t08 = new DateTimeOffset(lunes.ToDateTime(new TimeOnly(8, 0)), _offset);
        var t09 = new DateTimeOffset(lunes.ToDateTime(new TimeOnly(9, 0)), _offset);
        work.Store.AddBlock("consultorio_1", t08, t09, "Capacitación", "admin");

        // Bloqueo de 07:00 a 08:00 que termina exactamente en el borde del inicio de jornada (08:00)
        var t07 = new DateTimeOffset(lunes.ToDateTime(new TimeOnly(7, 0)), _offset);
        work.Store.AddBlock("consultorio_1", t07, t08, "Antes de abrir", "admin");

        work.Commit();

        using var queryWork = AgendaWork.Begin(_db, _sealer, _personKeys);
        var semana = WeekSummary.For(queryWork.Store, _setup, lunes, _offset);

        // Base de lunes sin bloqueos: consultorio_1 (10) + consultorio_2 (8) = 18.
        // Con bloqueo de 2 huecos en c1 y el de borde que no afecta: 18 - 2 = 16.
        Assert.Equal(16, semana[0].Capacity);
    }

    [Fact]
    public void Bloqueo_general_cierra_todos_los_recursos()
    {
        var lunes = new DateOnly(2026, 11, 16);
        var viernes = lunes.AddDays(4); // 2026-11-20
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);

        // Bloqueo general ("") de 08:00 a 13:00 el viernes
        var v08 = new DateTimeOffset(viernes.ToDateTime(new TimeOnly(8, 0)), _offset);
        var v13 = new DateTimeOffset(viernes.ToDateTime(new TimeOnly(13, 0)), _offset);
        work.Store.AddBlock(string.Empty, v08, v13, "Festivo", "admin");
        work.Commit();

        using var queryWork = AgendaWork.Begin(_db, _sealer, _personKeys);
        var semana = WeekSummary.For(queryWork.Store, _setup, lunes, _offset);

        // El viernes queda con capacidad 0
        Assert.Equal(0, semana[4].Capacity);
    }

    [Fact]
    public void Tramo_parcial_de_horario_cuenta_un_hueco_entero()
    {
        // Setup con horario de 45 minutos (08:00 a 08:45) con hueco de 30 min -> 2 pasos
        var parcial = new ResourceSpec("c_parcial", "Parcial", "Sala 1", ["control"], new Dictionary<DayOfWeek, IReadOnlyList<TimeRange>>
        {
            [DayOfWeek.Monday] = [new TimeRange(new TimeOnly(8, 0), new TimeOnly(8, 45))]
        });
        var customSetup = new AgendaSetup("parcial", 30, "Cliente", [_setup.Services[0]], [parcial]);

        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        var lunes = new DateOnly(2026, 11, 16);
        var semana = WeekSummary.For(work.Store, customSetup, lunes, _offset);

        Assert.Equal(2, semana[0].Capacity);
    }

    [Fact]
    public void Citas_ocupadas_redondean_hacia_arriba_y_canceladas_no_cuentan()
    {
        var lunes = new DateOnly(2026, 11, 16);
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        var c1 = work.People.Add("Luis", "3100000000");

        var t08 = new DateTimeOffset(lunes.ToDateTime(new TimeOnly(8, 0)), _offset);
        var t10 = new DateTimeOffset(lunes.ToDateTime(new TimeOnly(10, 0)), _offset);

        // Cita de 45 min en slots de 30 min: ceil(45 / 30) = 2 huecos
        work.Store.Add(c1.Id, "procedimiento", "consultorio_1", t08, 45, "u1");

        // Cita de 30 min pero cancelada: 0 huecos
        var app2 = work.Store.Add(c1.Id, "control", "consultorio_1", t10, 30, "u1");
        work.Store.Update(app2.Id, "u1", state: AppointmentState.Cancelled);

        work.Commit();

        using var queryWork = AgendaWork.Begin(_db, _sealer, _personKeys);
        var semana = WeekSummary.For(queryWork.Store, _setup, lunes, _offset);

        Assert.Equal(2, semana[0].Booked);
    }
}
