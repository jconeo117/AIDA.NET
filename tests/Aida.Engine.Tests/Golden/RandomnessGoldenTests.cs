using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Aida.Engine.Hashing;
using Aida.Engine.Randomness;

namespace Aida.Engine.Tests.Golden;

public sealed class RandomnessGoldenTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void El_mersenne_twister_coincide_con_la_01()
    {
        var lines = GoldenFiles.ReadLines("randomness/mt.jsonl");

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var seed = ulong.Parse(root.GetProperty("seed").GetString()!, CultureInfo.InvariantCulture);

            var expectedU32 = root.GetProperty("u32").EnumerateArray().Select(e => e.GetUInt32()).ToList();
            var expectedF = root.GetProperty("f").EnumerateArray()
                .Select(e => ulong.Parse(e.GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                .ToList();

            var mtU32 = MersenneTwister.FromSeed(seed);
            for (var i = 0; i < expectedU32.Count; i++)
            {
                var actual = mtU32.NextUInt32();
                Assert.True(
                    expectedU32[i] == actual,
                    $"Semilla {seed}, u32[{i}]: esperado {expectedU32[i]}, obtenido {actual}");
            }

            var mtF = MersenneTwister.FromSeed(seed);
            for (var i = 0; i < expectedF.Count; i++)
            {
                var actual = mtF.NextDouble();
                var actualBits = BitConverter.DoubleToUInt64Bits(actual);
                Assert.True(
                    expectedF[i] == actualBits,
                    $"Semilla {seed}, f[{i}]: esperado 0x{expectedF[i]:X16}, obtenido 0x{actualBits:X16}");
            }
        }
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void Las_claves_siembran_como_en_la_01()
    {
        var lines = GoldenFiles.ReadLines("randomness/streams.jsonl");

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var seed = root.GetProperty("seed").GetInt64();
            var key = root.GetProperty("key").EnumerateArray().Select(e => e.GetString()!).ToArray();
            var expectedMaterial = root.GetProperty("material").GetString()!;
            var expectedDigest = root.GetProperty("digest").GetString()!;
            var expectedF = root.GetProperty("f").EnumerateArray()
                .Select(e => ulong.Parse(e.GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                .ToList();

            var material = string.Join('\u001f', [seed.ToString(CultureInfo.InvariantCulture), .. key]);
            var matHex = Convert.ToHexStringLower(Encoding.UTF8.GetBytes(material));
            Assert.Equal(expectedMaterial, matHex);

            var digest = Blake2b.Hash(Encoding.UTF8.GetBytes(material), 8);
            var digestU64 = BinaryPrimitives.ReadUInt64BigEndian(digest);
            Assert.Equal(expectedDigest, digestU64.ToString(CultureInfo.InvariantCulture));

            var source = new RngSource(seed);
            var mt = source.Stream(key);
            for (var i = 0; i < expectedF.Count; i++)
            {
                var actual = mt.NextDouble();
                var actualBits = BitConverter.DoubleToUInt64Bits(actual);
                Assert.True(
                    expectedF[i] == actualBits,
                    $"Clave [{string.Join(',', key)}], f[{i}]: esperado 0x{expectedF[i]:X16}, obtenido 0x{actualBits:X16}");
            }
        }
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void El_sorteo_con_pesos_coincide_con_la_01()
    {
        var lines = GoldenFiles.ReadLines("randomness/choices.jsonl");

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var seed = ulong.Parse(root.GetProperty("seed").GetString()!, CultureInfo.InvariantCulture);
            var weights = root.GetProperty("w").EnumerateArray()
                .Select(e => BitConverter.UInt64BitsToDouble(ulong.Parse(e.GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture)))
                .ToArray();
            var expectedIdx = root.GetProperty("idx").GetInt32();

            var mt = MersenneTwister.FromSeed(seed);
            var actualIdx = WeightedChoice.Pick(mt, weights);
            Assert.Equal(expectedIdx, actualIdx);
        }
    }
}
