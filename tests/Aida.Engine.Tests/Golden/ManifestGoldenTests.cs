using System.Security.Cryptography;
using System.Text.Json;

namespace Aida.Engine.Tests.Golden;

public sealed class ManifestGoldenTests
{
    private const string ExpectedCommit = "9a96fb0fbfc882bd8b3062b1abad12b345ab9879";

    [Fact]
    [Trait("Category", "Golden")]
    public void El_manifiesto_cubre_los_dorados_y_sus_huellas_coinciden()
    {
        var manifestPath = Path.Combine(GoldenFiles.GoldenDir, "manifest.json");
        Assert.True(File.Exists(manifestPath), $"No existe el manifiesto en {manifestPath}");

        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement;

        var commit = root.GetProperty("source").GetProperty("commit").GetString();
        Assert.Equal(ExpectedCommit, commit);

        var filesProp = root.GetProperty("files");
        var diffs = new List<(int Line, string Expected, string Actual)>();
        var manifestFiles = new HashSet<string>(StringComparer.Ordinal);

        var index = 0;
        foreach (var fileProp in filesProp.EnumerateObject())
        {
            index++;
            var relativePath = fileProp.Name;
            var expectedSha = fileProp.Value.GetString()!;
            manifestFiles.Add(relativePath);

            var fullPath = Path.Combine(GoldenFiles.GoldenDir, relativePath);
            if (!File.Exists(fullPath))
            {
                diffs.Add((index, $"Archivo existente: {relativePath}", "Archivo no encontrado"));
                continue;
            }

            var actualSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(fullPath)));
            if (!string.Equals(actualSha, expectedSha, StringComparison.Ordinal))
            {
                diffs.Add((index, $"{relativePath} sha256={expectedSha}", $"{relativePath} sha256={actualSha}"));
            }
        }

        // Comprobar que en tests/golden no hay ningún archivo fuera del manifiesto (salvo manifest.json y generator/)
        var allFiles = Directory.GetFiles(GoldenFiles.GoldenDir, "*", SearchOption.AllDirectories);
        foreach (var file in allFiles)
        {
            var rel = Path.GetRelativePath(GoldenFiles.GoldenDir, file).Replace('\\', '/');
            if (rel == "manifest.json" || rel.StartsWith("generator/", StringComparison.Ordinal))
            {
                continue;
            }

            if (!manifestFiles.Contains(rel))
            {
                index++;
                diffs.Add((index, "Archivo registrado en manifest.json", $"Archivo huérfano en tests/golden: {rel}"));
            }
        }

        Assert.True(diffs.Count == 0, GoldenFiles.FormatDifferences(diffs));
    }
}
