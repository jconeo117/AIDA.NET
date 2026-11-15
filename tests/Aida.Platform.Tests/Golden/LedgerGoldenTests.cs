using System.Text.Json;
using Aida.Platform.Ledger;
using Xunit;

namespace Aida.Platform.Tests.Golden;

public sealed class LedgerGoldenTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void La_cadena_da_las_huellas_de_la_01()
    {
        var chainPath = Path.Combine(FindRepoRoot(), "tests", "golden", "ledger", "chain.tsv");
        Assert.True(File.Exists(chainPath), $"No existe el archivo dorado: {chainPath}");

        var lines = File.ReadAllLines(chainPath);
        Assert.NotEmpty(lines);

        var previousDigest = ChainedLog.Genesis;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var parts = line.Split('\t');
            Assert.True(parts.Length == 2, $"Línea {i + 1}: formato inválido (se esperaba json\\tdigest)");

            var expectedJson = parts[0];
            var expectedDigest = parts[1];

            using var doc = JsonDocument.Parse(expectedJson);
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.GetString()!;
            }

            Assert.True(dict.TryGetValue("prev", out var prev) && prev == previousDigest,
                $"Línea {i + 1}: prev esperado '{previousDigest}', obtenido '{prev}'");

            var actualJson = LedgerJson.Write(dict);
            Assert.True(actualJson == expectedJson,
                $"Línea {i + 1}: JSON esperado:\n{expectedJson}\npero obtenido:\n{actualJson}");

            var actualDigest = ChainedLog.Digest(dict);
            Assert.True(actualDigest == expectedDigest,
                $"Línea {i + 1}: huella esperada '{expectedDigest}', obtenida '{actualDigest}'");

            previousDigest = actualDigest;
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio que contiene Aida.slnx.");
    }
}
