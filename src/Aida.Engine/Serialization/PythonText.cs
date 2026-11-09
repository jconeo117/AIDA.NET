using System.Globalization;
using System.Numerics;
using System.Text;

namespace Aida.Engine.Serialization;

/// <summary>Escribe valores como lo hacen las f-strings de la 0.1 de Python.</summary>
/// <remarks>Los mensajes de AIDA se comparan literales con la 0.1 (ADR 0013), y llevan <c>{x!r}</c>,
/// <c>{decimal}</c> y <c>round(x, 4)</c>, que .NET escribe distinto.</remarks>
internal static class PythonText
{
    /// <summary><c>repr(s)</c> de un texto de Python (CPython, <c>unicode_repr</c>).</summary>
    /// <param name="value">El texto.</param>
    /// <returns>El texto entre comillas, escapado como lo escribe Python.</returns>
    internal static string Repr(string value)
    {
        var quote = value.Contains('\'') && !value.Contains('"') ? '"' : '\'';
        var builder = new StringBuilder().Append(quote);
        foreach (var rune in value.EnumerateRunes())
        {
            var c = rune.Value;
            if (c == quote || c == '\\')
            {
                builder.Append('\\').Append((char)c);
            }
            else if (c == '\t')
            {
                builder.Append("\\t");
            }
            else if (c == '\n')
            {
                builder.Append("\\n");
            }
            else if (c == '\r')
            {
                builder.Append("\\r");
            }
            else if (c < 0x20 || c == 0x7F)
            {
                builder.Append("\\x").Append(c.ToString("x2", CultureInfo.InvariantCulture));
            }
            else if (c < 0x7F || IsPrintable(rune))
            {
                builder.Append(rune.ToString());
            }
            else if (c <= 0xFF)
            {
                builder.Append("\\x").Append(c.ToString("x2", CultureInfo.InvariantCulture));
            }
            else if (c <= 0xFFFF)
            {
                builder.Append("\\u").Append(c.ToString("x4", CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append("\\U").Append(c.ToString("x8", CultureInfo.InvariantCulture));
            }
        }

        return builder.Append(quote).ToString();
    }

    // str.isprintable() de Python: no lo son Cc, Cf, Cs, Co, Cn, Zl, Zp ni Zs (salvo el espacio, que ya
    // salió por c < 0x7F). Python 3.12 usa Unicode 15.0; un carácter asignado después queda fuera del contrato.
    private static bool IsPrintable(Rune rune) => Rune.GetUnicodeCategory(rune) switch
    {
        UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate
            or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned
            or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
            or UnicodeCategory.SpaceSeparator => false,
        _ => true,
    };

    /// <summary><c>str(x)</c> de un decimal de Python, que es su <c>repr</c>.</summary>
    /// <param name="value">El decimal.</param>
    /// <returns>El decimal como lo escribe Python: <c>1.0</c>, <c>nan</c>, <c>inf</c>.</returns>
    internal static string Float(double value) =>
        double.IsNaN(value) ? "nan"
        : double.IsPositiveInfinity(value) ? "inf"
        : double.IsNegativeInfinity(value) ? "-inf"
        : CanonicalJson.FormatDouble(value);

    /// <summary><c>round(x, 4)</c> de Python: al par, sobre el valor binario exacto.</summary>
    /// <param name="value">El decimal.</param>
    /// <returns>El decimal redondeado a cuatro cifras.</returns>
    internal static double Round4(double value)
    {
        if (!double.IsFinite(value) || value == 0)
        {
            return value;
        }

        // value = mantissa · 2^exponent, exacto. Se calcula q = value · 10^4 redondeado al par, con enteros.
        var bits = BitConverter.DoubleToInt64Bits(Math.Abs(value));
        var rawExponent = (int)((bits >> 52) & 0x7FF);
        var mantissa = new BigInteger(bits & 0xFFFFFFFFFFFFFL);
        if (rawExponent != 0)
        {
            mantissa += BigInteger.One << 52;
        }

        var exponent = (rawExponent == 0 ? 1 : rawExponent) - 1075;
        var numerator = mantissa * 10_000;
        BigInteger q;
        if (exponent >= 0)
        {
            q = numerator << exponent;
        }
        else
        {
            var denominator = BigInteger.One << -exponent;
            q = BigInteger.DivRem(numerator, denominator, out var remainder);
            var twice = remainder * 2;
            if (twice > denominator || (twice == denominator && !q.IsEven))
            {
                q += 1;
            }
        }

        // q / 10^4 se convierte al double más cercano, como hace Python con el texto redondeado.
        var rounded = double.Parse($"{q}E-4", NumberStyles.Float, CultureInfo.InvariantCulture);
        return value < 0 ? -rounded : rounded;
    }
}
