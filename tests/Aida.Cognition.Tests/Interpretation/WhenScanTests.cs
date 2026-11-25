using Aida.Cognition.Interpretation;
using Aida.Engine.Text;
using Aida.Platform.Assistant;
using Xunit;

namespace Aida.Cognition.Tests.Interpretation;

public sealed class WhenScanTests
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
    public void IsDigits_valida_correctamente()
    {
        Assert.False(WhenScan.IsDigits(string.Empty));
        Assert.False(WhenScan.IsDigits("12a"));
        Assert.False(WhenScan.IsDigits("abc"));
        Assert.True(WhenScan.IsDigits("12345"));
    }

    [Fact]
    public void Metodos_de_scan_con_indices_fuera_de_rango_devuelven_valores_por_defecto()
    {
        var repoRoot = FindRepoRoot();
        var timeVocab = TimeVocabulary.Load(Path.Combine(repoRoot, "packs", "platform", "time.yaml"));
        var text = "hola 123";
        var spans = TextTokens.Spans(text);
        var scan = new WhenScan(text, spans, timeVocab, new DateOnly(2026, 11, 24));

        Assert.Equal(string.Empty, scan.Word(-1));
        Assert.Equal(string.Empty, scan.Word(10));
        Assert.Null(scan.Number(-1));
        Assert.Null(scan.Number(0)); // "hola" no es dígito
        Assert.Equal(123, scan.Number(1));
        Assert.Null(scan.Number(10));
        Assert.False(scan.HasGap(10, ":"));
    }
}
