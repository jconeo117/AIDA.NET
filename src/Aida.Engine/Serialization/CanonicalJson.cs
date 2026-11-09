using System.Globalization;
using System.Text;

namespace Aida.Engine.Serialization;

/// <summary>Escribe JSON exactamente como lo escribe la 0.1 de Python.</summary>
/// <remarks>ADR 0023: claves por punto de código, sin espacios, escapes de <c>json.dumps(ensure_ascii=False)</c>
/// y decimales con el formato de <c>repr</c>. La biblioteca JSON de la plataforma no sirve: escapa y formatea distinto.</remarks>
internal static class CanonicalJson
{
    /// <summary>Escribe una cadena entre comillas, con los escapes de la 0.1.</summary>
    /// <param name="builder">Donde se escribe.</param>
    /// <param name="value">La cadena.</param>
    internal static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;

                // U+2028 y U+2029 son saltos de línea para str.splitlines(): json.dumps los deja crudos y la 0.1 los escapa a mano.
                case (char)0x2028: builder.Append("\\u2028"); break;
                case (char)0x2029: builder.Append("\\u2029"); break;
                default:
                    if (c < ' ')
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }

    /// <summary>Escribe un entero en decimal.</summary>
    /// <param name="value">El entero.</param>
    /// <returns>El entero en decimal, sin separadores.</returns>
    internal static string FormatInteger(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Escribe un decimal con el formato de <c>repr</c> de Python.</summary>
    /// <param name="value">El decimal.</param>
    /// <returns>El decimal, como <c>0.1</c>, <c>1.0</c>, <c>1e-05</c> o <c>NaN</c>.</returns>
    internal static string FormatDouble(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "Infinity";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        // La cadena más corta que recupera el mismo valor; solo se recolocan la coma y el exponente.
        var shortest = value.ToString("R", CultureInfo.InvariantCulture);
        var negative = shortest[0] == '-';
        if (negative)
        {
            shortest = shortest[1..];
        }

        var exponent = 0;
        var e = shortest.IndexOf('E', StringComparison.Ordinal);
        if (e >= 0)
        {
            exponent = int.Parse(shortest[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            shortest = shortest[..e];
        }

        var dot = shortest.IndexOf('.', StringComparison.Ordinal);
        var digits = dot >= 0 ? string.Concat(shortest.AsSpan(0, dot), shortest.AsSpan(dot + 1)) : shortest;
        var point = dot >= 0 ? dot : shortest.Length;
        var lead = 0;
        while (lead < digits.Length - 1 && digits[lead] == '0')
        {
            lead++;
            point--;
        }

        digits = digits[lead..].TrimEnd('0');
        var sign = negative ? "-" : string.Empty;
        if (digits.Length == 0)
        {
            return $"{sign}0.0";
        }

        // decimalPoint es el «decpt» de Python: dónde cae la coma respecto al primer dígito.
        var decimalPoint = point + exponent;
        if (decimalPoint <= -4 || decimalPoint > 16)
        {
            var shown = decimalPoint - 1;
            var mantissa = digits.Length > 1 ? $"{digits[0]}.{digits[1..]}" : digits;
            return $"{sign}{mantissa}e{(shown < 0 ? '-' : '+')}{Math.Abs(shown).ToString("00", CultureInfo.InvariantCulture)}";
        }

        if (decimalPoint <= 0)
        {
            return $"{sign}0.{new string('0', -decimalPoint)}{digits}";
        }

        if (decimalPoint >= digits.Length)
        {
            return $"{sign}{digits}{new string('0', decimalPoint - digits.Length)}.0";
        }

        return $"{sign}{digits[..decimalPoint]}.{digits[decimalPoint..]}";
    }
}
