using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Platform.Packs;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Desk;

public sealed class DayViewTests
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

    [Fact]
    public void BlockAt_Devuelve_el_primero_que_aplica_o_null_y_respeta_recurso()
    {
        var repoRoot = FindRepoRoot();
        var consultorioPath = Path.Combine(repoRoot, "packs", "agenda", "consultorio", "agenda.yaml");
        var setup = AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(consultorioPath), consultorioPath);

        var day = new DateOnly(2026, 11, 16);
        var now = DateTimeOffset.Parse("2026-11-16T07:30:00-05:00");

        var t10 = DateTimeOffset.Parse("2026-11-16T10:00:00-05:00");
        var t11 = DateTimeOffset.Parse("2026-11-16T11:00:00-05:00");
        var t12 = DateTimeOffset.Parse("2026-11-16T12:00:00-05:00");

        var blockR1 = new Block(1, "consultorio_1", t10, t11, "Bloqueo R1", "u1");
        var blockR2 = new Block(2, "consultorio_2", t10, t11, "Bloqueo R2", "u1");
        var blockAll = new Block(3, string.Empty, t11, t12, "Cierre Total", "admin");

        var blocks = new List<BlockView>
        {
            new(blockR1, "Usuario 1"),
            new(blockR2, "Usuario 1"),
            new(blockAll, "Admin")
        };

        var dayView = new DayView(day, now, setup, [], blocks, new Dictionary<string, IReadOnlyList<FreeSlot>>());

        // 1. Consulta para consultorio_1 entre 10:00 y 10:30 -> encuentra blockR1
        var atR1 = dayView.BlockAt("consultorio_1", t10, t10.AddMinutes(30));
        Assert.NotNull(atR1);
        Assert.Equal(1, atR1.Block.Id);

        // 2. Consulta para consultorio_2 entre 10:00 y 10:30 -> encuentra blockR2
        var atR2 = dayView.BlockAt("consultorio_2", t10, t10.AddMinutes(30));
        Assert.NotNull(atR2);
        Assert.Equal(2, atR2.Block.Id);

        // 3. Consulta para consultorio_3 entre 10:00 y 10:30 -> no aplica ninguno de los dos (null)
        var atR3 = dayView.BlockAt("consultorio_3", t10, t10.AddMinutes(30));
        Assert.Null(atR3);

        // 4. Consulta para consultorio_3 entre 11:00 y 11:30 -> encuentra blockAll (aplica a todos)
        var atAll = dayView.BlockAt("consultorio_3", t11, t11.AddMinutes(30));
        Assert.NotNull(atAll);
        Assert.Equal(3, atAll.Block.Id);

        // 5. Consulta en tramo sin bloqueos (08:00 a 09:00) -> null
        var atFree = dayView.BlockAt("consultorio_1", DateTimeOffset.Parse("2026-11-16T08:00:00-05:00"), DateTimeOffset.Parse("2026-11-16T09:00:00-05:00"));
        Assert.Null(atFree);
    }
}
