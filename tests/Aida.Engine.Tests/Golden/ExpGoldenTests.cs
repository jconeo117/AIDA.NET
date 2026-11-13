using System.Globalization;
using Aida.Engine.Numerics;

namespace Aida.Engine.Tests.Golden;

public sealed class ExpGoldenTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void La_exponencial_coincide_con_la_01()
    {
        var lines = GoldenFiles.ReadLines("numerics/exp.txt");
        var lineIndex = 0;

        foreach (var line in lines)
        {
            lineIndex++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.True(parts.Length == 2, $"Línea {lineIndex} no tiene 2 columnas: {line}");

            var hexX = parts[0];
            var expectedResult = parts[1];

            var ubitsX = ulong.Parse(hexX, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var x = BitConverter.UInt64BitsToDouble(ubitsX);

            if (expectedResult == "OverflowError")
            {
                var ex = Assert.Throws<OverflowException>(() => PyFloat.Exp(x));
                Assert.Equal("math range error", ex.Message);
            }
            else
            {
                var expectedBits = ulong.Parse(expectedResult, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var actual = PyFloat.Exp(x);
                var actualBits = BitConverter.DoubleToUInt64Bits(actual);

                Assert.True(
                    expectedBits == actualBits,
                    $"Línea {lineIndex}: x=0x{ubitsX:X16} ({x:R}) esperado=0x{expectedBits:X16} obtenido=0x{actualBits:X16}");
            }
        }
    }
}
