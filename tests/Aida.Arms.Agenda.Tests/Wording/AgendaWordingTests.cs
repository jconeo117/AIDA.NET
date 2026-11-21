using Aida.Arms.Agenda.Wording;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Wording;

public sealed class AgendaWordingTests
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
    public void Lee_el_ui_yaml_del_repositorio()
    {
        var repoRoot = FindRepoRoot();
        var uiPath = Path.Combine(repoRoot, "packs", "agenda", "ui.yaml");

        var wording = AgendaWording.Load(uiPath);
        Assert.NotNull(wording);

        foreach (var key in AgendaWording.RequiredKeys)
        {
            Assert.NotEmpty(wording[key]);
        }
    }

    [Fact]
    public void Falta_un_texto_y_lo_dice()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                agenda:
                  title: Agenda
                """);
            var ex = Assert.Throws<InvalidDataException>(() => AgendaWording.Load(tempFile));
            Assert.StartsWith(tempFile, ex.Message);
            Assert.Contains("faltan textos de la agenda", ex.Message);
            Assert.Contains("new_button", ex.Message);
            Assert.Contains("conflicts_title", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Un_ui_yaml_sin_conflicts_title_falla_al_cargar()
    {
        var repoRoot = FindRepoRoot();
        var uiPath = Path.Combine(repoRoot, "packs", "agenda", "ui.yaml");
        var content = File.ReadAllText(uiPath);
        var withoutConflicts = content.Replace("conflicts_title:", "ignored_conflicts_title:");

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, withoutConflicts);
            var ex = Assert.Throws<InvalidDataException>(() => AgendaWording.Load(tempFile));
            Assert.Contains("conflicts_title", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Un_motivo_sin_texto_usa_reason_failed()
    {
        var repoRoot = FindRepoRoot();
        var uiPath = Path.Combine(repoRoot, "packs", "agenda", "ui.yaml");
        var wording = AgendaWording.Load(uiPath);

        // Motivo que existe en ui.yaml
        var known = wording.Reason("forbidden");
        Assert.Equal(wording["reason_forbidden"], known);

        // Motivo inexistente devuelve el texto de reason_failed
        var unknown = wording.Reason("motivo_completamente_desconocido");
        Assert.Equal(wording["reason_failed"], unknown);
    }
}
