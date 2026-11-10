using Aida.Engine.Hashing;

namespace Aida.Engine.Tests.Golden;

public sealed class Blake2bGoldenTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void Blake2b_coincide_con_la_01_y_con_el_RFC()
    {
        var lines = GoldenFiles.ReadLines("hashing/blake2b.txt");
        var diffs = new List<(int Line, string Expected, string Actual)>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(' ');
            Assert.Equal(3, parts.Length);

            var size = int.Parse(parts[0]);
            var hexInput = parts[1];
            var expectedDigest = parts[2];

            var input = Convert.FromHexString(hexInput);
            var actualDigest = Convert.ToHexStringLower(Blake2b.Hash(input, size));

            if (!string.Equals(actualDigest, expectedDigest, StringComparison.Ordinal))
            {
                diffs.Add((i + 1, expectedDigest, actualDigest));
            }
        }

        Assert.True(diffs.Count == 0, GoldenFiles.FormatDifferences(diffs));
    }
}
