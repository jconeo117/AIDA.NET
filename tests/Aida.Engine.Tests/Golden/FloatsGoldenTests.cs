using System.Globalization;
using Aida.Engine.Serialization;

namespace Aida.Engine.Tests.Golden;

public sealed class FloatsGoldenTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void Los_decimales_se_escriben_como_en_la_01()
    {
        var lines = GoldenFiles.ReadLines("serialization/floats.txt");
        var diffs = new List<(int Line, string Expected, string Actual)>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var spaceIndex = line.IndexOf(' ');
            Assert.True(spaceIndex > 0, $"Línea {i + 1} no tiene espacio separador: {line}");

            var hexBits = line[..spaceIndex];
            var expectedJson = line[(spaceIndex + 1)..];

            var ubits = ulong.Parse(hexBits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var value = BitConverter.UInt64BitsToDouble(ubits);
            var actualJson = CanonicalJson.FormatDouble(value);

            if (!string.Equals(actualJson, expectedJson, StringComparison.Ordinal))
            {
                diffs.Add((i + 1, expectedJson, actualJson));
            }
        }

        Assert.True(diffs.Count == 0, GoldenFiles.FormatDifferences(diffs));
    }
}
