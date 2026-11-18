using Aida.Arms.Agenda.Setup;
using Aida.Engine.Ontology;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Setup;

public sealed class AgendaManifestTests
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
    public void Lee_el_arm_yaml_del_repositorio()
    {
        var repoRoot = FindRepoRoot();
        var armPath = Path.Combine(repoRoot, "packs", "agenda", "arm.yaml");

        var manifest = AgendaManifest.Load(armPath);
        Assert.Equal("consultorio", manifest.DefaultSector);
        Assert.Equal(5, manifest.Actions.Count);

        string[] actions = ["agendar", "reprogramar", "cancelar", "atendida", "no_asistio"];
        foreach (var action in actions)
        {
            Assert.True(manifest.Actions.TryGet(new ActionId(action), out var reversibility));
            Assert.Equal(Reversibility.Reversible, reversibility);
        }
    }

    [Fact]
    public void Sin_actions_falla()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "default_sector: consultorio\n");
            var ex = Assert.Throws<InvalidDataException>(() => AgendaManifest.Load(tempFile));
            Assert.Contains("falta la lista 'actions'", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
