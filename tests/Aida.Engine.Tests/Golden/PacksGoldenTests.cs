using System.Text.Json;
using Aida.Engine.Packs;

namespace Aida.Engine.Tests.Golden;

public sealed class PacksGoldenTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void Los_packs_reales_se_cargan_como_en_la_01()
    {
        var diffs = new List<(int Line, string Expected, string Actual)>();
        var cases = new[]
        {
            (
                Name: "plantilla",
                YamlPath: Path.Combine(GoldenFiles.RepoRoot, "packs", "_plantilla", "pack.yaml"),
                JsonPath: Path.Combine(GoldenFiles.GoldenDir, "packs", "plantilla.json")
            ),
            (
                Name: "consultorio",
                YamlPath: Path.Combine(GoldenFiles.RepoRoot, "packs", "health", "consultorio", "pack.yaml"),
                JsonPath: Path.Combine(GoldenFiles.GoldenDir, "packs", "consultorio.json")
            ),
        };

        var index = 0;
        foreach (var c in cases)
        {
            index++;
            Assert.True(File.Exists(c.YamlPath), $"No existe el pack en {c.YamlPath}");
            Assert.True(File.Exists(c.JsonPath), $"No existe el dorado en {c.JsonPath}");

            var pack = PackLoader.Load(c.YamlPath);
            var actualDump = PackDump.Dump(pack);
            var expectedDump = File.ReadAllText(c.JsonPath).TrimEnd('\r', '\n');

            if (!string.Equals(actualDump, expectedDump, StringComparison.Ordinal))
            {
                diffs.Add((index, $"{c.Name}: esperado ({expectedDump.Length} bytes)", $"{c.Name}: obtenido ({actualDump.Length} bytes)"));
            }
        }

        Assert.True(diffs.Count == 0, GoldenFiles.FormatDifferences(diffs));
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void Los_packs_rotos_fallan_como_en_la_01()
    {
        var casesPath = Path.Combine(GoldenFiles.GoldenDir, "packs", "cases.jsonl");
        Assert.True(File.Exists(casesPath), $"No existe el archivo de casos en {casesPath}");

        var lines = File.ReadAllLines(casesPath);
        var diffs = new List<(int Line, string Expected, string Actual)>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var name = root.GetProperty("name").GetString()!;
            var yaml = root.GetProperty("yaml").GetString()!;
            var expect = root.GetProperty("expect");

            if (expect.TryGetProperty("dump", out var dumpProp))
            {
                var expectedDump = dumpProp.GetString()!;
                try
                {
                    var pack = PackLoader.Parse(yaml);
                    var actualDump = PackDump.Dump(pack);
                    if (!string.Equals(actualDump, expectedDump, StringComparison.Ordinal))
                    {
                        diffs.Add((i + 1, $"[{name}] dump esperado: {expectedDump[..Math.Min(60, expectedDump.Length)]}...", $"dump obtenido: {actualDump[..Math.Min(60, actualDump.Length)]}..."));
                    }
                }
                catch (Exception ex)
                {
                    diffs.Add((i + 1, $"[{name}] carga exitosa", $"excepción inesperada: {ex.Message}"));
                }
            }
            else if (expect.TryGetProperty("errors", out var errorsProp))
            {
                var expectedErrors = errorsProp.EnumerateArray()
                    .Select(e => (
                        Path: e.GetProperty("path").GetString()!,
                        Message: e.GetProperty("message").ValueKind == JsonValueKind.Null ? null : e.GetProperty("message").GetString()
                    ))
                    .ToList();

                try
                {
                    PackLoader.Parse(yaml);
                    diffs.Add((i + 1, $"[{name}] fallo con {expectedErrors.Count} error(es)", "el pack cargó sin errores"));
                }
                catch (PackException ex)
                {
                    var actualErrors = ex.Problems
                        .Select(p => (Path: p.Path, Message: p.FromAida ? p.Message : null))
                        .ToList();

                    var match = expectedErrors.Count == actualErrors.Count;
                    if (match)
                    {
                        for (var k = 0; k < expectedErrors.Count; k++)
                        {
                            if (!string.Equals(expectedErrors[k].Path, actualErrors[k].Path, StringComparison.Ordinal)
                                || !string.Equals(expectedErrors[k].Message, actualErrors[k].Message, StringComparison.Ordinal))
                            {
                                match = false;
                                break;
                            }
                        }
                    }

                    if (!match)
                    {
                        var expStr = string.Join("; ", expectedErrors.Select(e => $"({e.Path}, {e.Message ?? "null"})"));
                        var actStr = string.Join("; ", actualErrors.Select(e => $"({e.Path}, {e.Message ?? "null"})"));
                        diffs.Add((i + 1, $"[{name}] {expStr}", actStr));
                    }
                }
                catch (Exception ex)
                {
                    diffs.Add((i + 1, $"[{name}] PackException", $"excepción inesperada: {ex.GetType().Name}: {ex.Message}"));
                }
            }
            else if (expect.TryGetProperty("problems", out var problemsProp))
            {
                var expectedProblems = problemsProp.EnumerateArray()
                    .Select(e => e.GetString()!)
                    .ToList();

                try
                {
                    PackLoader.Parse(yaml);
                    diffs.Add((i + 1, $"[{name}] fallo con {expectedProblems.Count} problema(s)", "el pack cargó sin problemas"));
                }
                catch (PackException ex)
                {
                    var allFromAidaAndEmptyPath = ex.Problems.All(p => p.FromAida && p.Path.Length == 0);
                    var actualProblems = ex.Problems.Select(p => p.Message).ToList();

                    var match = allFromAidaAndEmptyPath && expectedProblems.SequenceEqual(actualProblems, StringComparer.Ordinal);
                    if (!match)
                    {
                        var expStr = string.Join(" | ", expectedProblems);
                        var actStr = string.Join(" | ", actualProblems);
                        diffs.Add((i + 1, $"[{name}] {expStr}", $"FromAida&Empty={allFromAidaAndEmptyPath}: {actStr}"));
                    }
                }
                catch (Exception ex)
                {
                    diffs.Add((i + 1, $"[{name}] PackException", $"excepción inesperada: {ex.GetType().Name}: {ex.Message}"));
                }
            }
        }

        Assert.True(diffs.Count == 0, GoldenFiles.FormatDifferences(diffs));
    }
}
