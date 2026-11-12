using System.Globalization;
using System.Text;
using Aida.Engine.Numerics;

namespace Aida.Engine.Tests.Golden;

public sealed class PowGoldenTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void La_potencia_coincide_con_la_01()
    {
        var lines = GoldenFiles.ReadLines("numerics/pow.txt");
        var diffs = new List<(int Line, string X, string Y, string Expected, string Actual)>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.True(parts.Length == 3, $"Línea {i + 1} no tiene 3 columnas: {line}");

            var hexX = parts[0];
            var hexY = parts[1];
            var expectedResult = parts[2];

            var ubitsX = ulong.Parse(hexX, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var ubitsY = ulong.Parse(hexY, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var x = BitConverter.UInt64BitsToDouble(ubitsX);
            var y = BitConverter.UInt64BitsToDouble(ubitsY);

            string actualResult;
            try
            {
                var r = PyFloat.Pow(x, y);
                var actualBits = BitConverter.DoubleToUInt64Bits(r);
                actualResult = $"{actualBits:X16}";
            }
            catch (OverflowException)
            {
                actualResult = "OverflowError";
            }
            catch (DivideByZeroException)
            {
                actualResult = "ZeroDivisionError";
            }
            catch (ArithmeticException)
            {
                actualResult = "complex";
            }

            if (!string.Equals(actualResult, expectedResult, StringComparison.Ordinal))
            {
                diffs.Add((i + 1, hexX, hexY, expectedResult, actualResult));
            }
        }

        if (diffs.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Se encontraron {diffs.Count} diferencias en pow.txt. Mostrando hasta 10:");
            for (var i = 0; i < Math.Min(diffs.Count, 10); i++)
            {
                var (line, hx, hy, exp, act) = diffs[i];
                sb.AppendLine($"[Línea {line}] x=0x{hx} y=0x{hy} -> Esperado: {exp}, Obtenido: {act}");
            }

            Assert.Fail(sb.ToString());
        }
    }
}
