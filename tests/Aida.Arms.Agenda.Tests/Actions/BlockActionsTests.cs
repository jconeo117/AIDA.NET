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

public sealed class BlockActionsTests : IDisposable
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

    public BlockActionsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "aida-blockactions-" + Guid.NewGuid().ToString("n"));
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
    public void Block_Reglas_de_argumentos_recurso_y_pasado()
    {
        using var work = Begin();
        var now = DateTimeOffset.Parse("2026-11-16T10:00:00-05:00");
        var s11 = "2026-11-16T11:00:00-05:00";
        var s12 = "2026-11-16T12:00:00-05:00";

        // 1. Fechas inválidas o faltantes -> bad_argument
        var rNoStart = BlockActions.Block(work.Store, _setup, now, new Dictionary<string, string>
        {
            ["end"] = s12, ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rNoStart.Status);
        Assert.Equal("bad_argument", rNoStart.Reason);

        var rInvalidFormat = BlockActions.Block(work.Store, _setup, now, new Dictionary<string, string>
        {
            ["start"] = "2026-11-16 11:00", ["end"] = s12, ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rInvalidFormat.Status);
        Assert.Equal("bad_argument", rInvalidFormat.Reason);

        // 2. end <= start -> bad_argument
        var rInverted = BlockActions.Block(work.Store, _setup, now, new Dictionary<string, string>
        {
            ["start"] = s12, ["end"] = s11, ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rInverted.Status);
        Assert.Equal("bad_argument", rInverted.Reason);

        var rEqual = BlockActions.Block(work.Store, _setup, now, new Dictionary<string, string>
        {
            ["start"] = s11, ["end"] = s11, ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rEqual.Status);
        Assert.Equal("bad_argument", rEqual.Reason);

        // 3. Recurso desconocido -> unknown_resource
        var rUnknownRes = BlockActions.Block(work.Store, _setup, now, new Dictionary<string, string>
        {
            ["start"] = s11, ["end"] = s12, ["resource"] = "desconocido", ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rUnknownRes.Status);
        Assert.Equal("unknown_resource", rUnknownRes.Reason);

        // 4. Fin antes de ahora o igual a ahora es past
        var rPastBefore = BlockActions.Block(work.Store, _setup, now, new Dictionary<string, string>
        {
            ["start"] = "2026-11-16T08:00:00-05:00", ["end"] = "2026-11-16T09:00:00-05:00", ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rPastBefore.Status);
        Assert.Equal("past", rPastBefore.Reason);

        var rPastEqual = BlockActions.Block(work.Store, _setup, now, new Dictionary<string, string>
        {
            ["start"] = "2026-11-16T09:00:00-05:00", ["end"] = "2026-11-16T10:00:00-05:00", ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rPastEqual.Status);
        Assert.Equal("past", rPastEqual.Reason);
    }

    [Fact]
    public void Block_Citas_agendadas_dentro_rechazan_con_has_appointments()
    {
        using var work = Begin();
        var now = DateTimeOffset.Parse("2026-11-16T05:00:00-05:00");
        var client = work.People.Add("Paciente", "3001112233");

        // Cita 1: de la víspera (15 nov 23:45 a 16 nov 00:15)
        var tVispera = DateTimeOffset.Parse("2026-11-15T23:45:00-05:00");
        var appVispera = work.Store.Add(client.Id, "control", "consultorio_1", tVispera, 30, "u1");

        // Bloqueo que inicia a las 00:00 del 16 nov (acaba a las 06:00, después de now a las 05:00): debe detectar la cita de la víspera
        var rVispera = BlockActions.Block(work.Store, _setup, now, new Dictionary<string, string>
        {
            ["start"] = "2026-11-16T00:00:00-05:00",
            ["end"] = "2026-11-16T06:00:00-05:00",
            ["resource"] = "consultorio_1",
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Rejected, rVispera.Status);
        Assert.Equal("has_appointments", rVispera.Reason);
        Assert.Equal(appVispera.Id.ToString(), rVispera.Data["appointments"]);

        // Cita 2: cancelada no estorba
        var t8 = DateTimeOffset.Parse("2026-11-16T08:00:00-05:00");
        var appCancelada = work.Store.Add(client.Id, "control", "consultorio_1", t8, 30, "u1");
        work.Store.Update(appCancelada.Id, "u1", state: AppointmentState.Cancelled);

        // Cita 3: atendida no estorba
        var t830 = DateTimeOffset.Parse("2026-11-16T08:30:00-05:00");
        var appAtendida = work.Store.Add(client.Id, "control", "consultorio_1", t830, 30, "u1");
        work.Store.Update(appAtendida.Id, "u1", state: AppointmentState.Attended);

        // Cita 4: en consultorio_2 (otro recurso) no estorba para bloqueo de consultorio_1
        var t9 = DateTimeOffset.Parse("2026-11-16T09:00:00-05:00");
        work.Store.Add(client.Id, "control", "consultorio_2", t9, 30, "u1");

        // Bloqueo de consultorio_1 de 08:00 a 09:30: ni cancelada, ni atendida, ni consultorio_2 estorban -> tiene éxito
        var rOk = BlockActions.Block(work.Store, _setup, now, new Dictionary<string, string>
        {
            ["start"] = "2026-11-16T08:00:00-05:00",
            ["end"] = "2026-11-16T09:30:00-05:00",
            ["resource"] = "consultorio_1",
            ["reason"] = "  Mantenimiento  ",
            ["by"] = "u1"
        });
        Assert.Equal(ToolStatus.Done, rOk.Status);
        Assert.True(rOk.Data.ContainsKey("block"));
    }

    [Fact]
    public void Unblock_Valida_id_y_borra()
    {
        using var work = Begin();
        var now = DateTimeOffset.Parse("2026-11-16T07:00:00-05:00");

        // Id no numérico -> unknown_block
        var rNoNum = BlockActions.Unblock(work.Store, new Dictionary<string, string> { ["block"] = "abc" });
        Assert.Equal(ToolStatus.Rejected, rNoNum.Status);
        Assert.Equal("unknown_block", rNoNum.Reason);

        // Id inexistente -> unknown_block
        var rInexistente = BlockActions.Unblock(work.Store, new Dictionary<string, string> { ["block"] = "999" });
        Assert.Equal(ToolStatus.Rejected, rInexistente.Status);
        Assert.Equal("unknown_block", rInexistente.Reason);

        // Crear bloqueo
        var rBlock = BlockActions.Block(work.Store, _setup, now, new Dictionary<string, string>
        {
            ["start"] = "2026-11-16T12:00:00-05:00",
            ["end"] = "2026-11-16T13:00:00-05:00",
            ["by"] = "u1"
        });
        var blockId = rBlock.Data["block"];

        // Desbloquear existente -> éxito
        var rUnblock = BlockActions.Unblock(work.Store, new Dictionary<string, string> { ["block"] = blockId });
        Assert.Equal(ToolStatus.Done, rUnblock.Status);
        Assert.Equal(blockId, rUnblock.Data["block"]);

        // Desbloquear de nuevo -> unknown_block
        var rUnblockAgain = BlockActions.Unblock(work.Store, new Dictionary<string, string> { ["block"] = blockId });
        Assert.Equal(ToolStatus.Rejected, rUnblockAgain.Status);
        Assert.Equal("unknown_block", rUnblockAgain.Reason);
    }
}
