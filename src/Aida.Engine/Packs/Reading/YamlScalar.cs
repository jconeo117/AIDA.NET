using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Resuelve los escalares planos como PyYAML 6.0.3, que es lo que la 0.1 ve antes que pydantic.</summary>
/// <remarks>
/// Las expresiones y las conversiones son las de <c>yaml/resolver.py</c> y <c>constructor.py</c> (§A.7). Son
/// reglas de YAML 1.1: <c>yes</c> es un booleano y <c>1e5</c> es un texto. Sin reproducirlas, un pack cargaría
/// distinto en las dos versiones (ADR 0024).
/// </remarks>
internal static class YamlScalar
{
    private const RegexOptions _options = RegexOptions.CultureInvariant;

    private static readonly Regex _boolPattern = new(
        "^(?:yes|Yes|YES|no|No|NO|true|True|TRUE|false|False|FALSE|on|On|ON|off|Off|OFF)$", _options);

    private static readonly Regex _floatPattern = new(
        @"^(?:[-+]?(?:[0-9][0-9_]*)\.[0-9_]*(?:[eE][-+][0-9]+)?|\.[0-9][0-9_]*(?:[eE][-+][0-9]+)?|[-+]?[0-9][0-9_]*(?::[0-5]?[0-9])+\.[0-9_]*|[-+]?\.(?:inf|Inf|INF)|\.(?:nan|NaN|NAN))$",
        _options);

    private static readonly Regex _intPattern = new(
        @"^(?:[-+]?0b[0-1_]+|[-+]?0[0-7_]+|[-+]?(?:0|[1-9][0-9_]*)|[-+]?0x[0-9a-fA-F_]+|[-+]?[1-9][0-9_]*(?::[0-5]?[0-9])+)$",
        _options);

    private static readonly Regex _nullPattern = new("^(?:~|null|Null|NULL|)$", _options);

    private static readonly Regex _timestampPattern = new(
        @"^(?:[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]|[0-9][0-9][0-9][0-9]-[0-9][0-9]?-[0-9][0-9]?(?:[Tt]|[ \t]+)[0-9][0-9]?:[0-9][0-9]:[0-9][0-9](?:\.[0-9]*)?(?:[ \t]*(?:Z|[-+][0-9][0-9]?(?::[0-9][0-9])?))?)$",
        _options);

    /// <summary>El texto de un escalar, con su valor vacío si YamlDotNet no lo informa.</summary>
    /// <param name="node">El escalar.</param>
    /// <returns>El valor del escalar, nunca <see langword="null"/>.</returns>
    internal static string TextOf(YamlScalarNode node) => node.Value ?? string.Empty;

    /// <summary>Qué tipo da PyYAML a un escalar.</summary>
    /// <param name="node">El escalar.</param>
    /// <returns>El tipo resuelto. Un escalar que no es plano es siempre texto.</returns>
    internal static ScalarKind KindOf(YamlScalarNode node)
    {
        if (node.Style != ScalarStyle.Plain)
        {
            return ScalarKind.Text;
        }

        var text = TextOf(node);
        if (_boolPattern.IsMatch(text))
        {
            return ScalarKind.Bool;
        }

        if (_floatPattern.IsMatch(text))
        {
            return ScalarKind.Float;
        }

        if (_intPattern.IsMatch(text))
        {
            return ScalarKind.Int;
        }

        if (_nullPattern.IsMatch(text))
        {
            return ScalarKind.Null;
        }

        return _timestampPattern.IsMatch(text) ? ScalarKind.Timestamp : ScalarKind.Text;
    }

    /// <summary>El valor booleano de un escalar que PyYAML resolvió como booleano.</summary>
    /// <param name="text">El texto del escalar.</param>
    /// <returns><see langword="true"/> para los de verdad, <see langword="false"/> para los de mentira.</returns>
    internal static bool BoolOf(string text) => text is "yes" or "Yes" or "YES" or "true" or "True" or "TRUE" or "on" or "On" or "ON";

    /// <summary>El valor de un entero que PyYAML resolvió como entero (<c>construct_yaml_int</c>).</summary>
    /// <param name="text">El texto del escalar.</param>
    /// <param name="value">El entero, si cabe en 64 bits.</param>
    /// <returns><see langword="false"/> si no cabe en 64 bits (ADR 0024).</returns>
    internal static bool TryIntOf(string text, out long value)
    {
        var digits = text.Replace("_", string.Empty, StringComparison.Ordinal);
        var sign = 1;
        if (digits[0] is '-' or '+')
        {
            sign = digits[0] == '-' ? -1 : 1;
            digits = digits[1..];
        }

        BigInteger number;
        if (digits == "0")
        {
            number = BigInteger.Zero;
        }
        else if (digits.StartsWith("0b", StringComparison.Ordinal))
        {
            number = ParseBase(digits[2..], 2);
        }
        else if (digits.StartsWith("0x", StringComparison.Ordinal))
        {
            number = ParseBase(digits[2..], 16);
        }
        else if (digits[0] == '0')
        {
            number = ParseBase(digits, 8);
        }
        else if (digits.Contains(':'))
        {
            number = ParseSexagesimalInt(digits);
        }
        else
        {
            number = ParseBase(digits, 10);
        }

        number *= sign;
        value = 0;
        if (number < long.MinValue || number > long.MaxValue)
        {
            return false;
        }

        value = (long)number;
        return true;
    }

    /// <summary>El valor de un decimal que PyYAML resolvió como decimal (<c>construct_yaml_float</c>).</summary>
    /// <param name="text">El texto del escalar.</param>
    /// <returns>El decimal, con <c>NaN</c> y los infinitos de <c>.nan</c> y <c>.inf</c>.</returns>
    internal static double FloatOf(string text)
    {
        var digits = text.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        var sign = 1.0;
        if (digits[0] is '-' or '+')
        {
            sign = digits[0] == '-' ? -1.0 : 1.0;
            digits = digits[1..];
        }

        if (digits == ".inf")
        {
            return sign * double.PositiveInfinity;
        }

        if (digits == ".nan")
        {
            return double.NaN;
        }

        if (digits.Contains(':'))
        {
            return sign * ParseSexagesimalFloat(digits);
        }

        return sign * double.Parse(digits, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static BigInteger ParseBase(string digits, int radix)
    {
        var value = BigInteger.Zero;
        foreach (var character in digits)
        {
            value = value * radix + DigitValue(character);
        }

        return value;
    }

    private static BigInteger ParseSexagesimalInt(string text)
    {
        var value = BigInteger.Zero;
        foreach (var part in text.Split(':'))
        {
            value = value * 60 + ParseBase(part, 10);
        }

        return value;
    }

    // Como PyYAML: se suma de la cifra menos significativa a la más significativa, en ese orden, para que el redondeo coincida.
    private static double ParseSexagesimalFloat(string text)
    {
        var parts = text.Split(':').Reverse().ToArray();
        var value = 0.0;
        var place = 1.0;
        foreach (var part in parts)
        {
            value += double.Parse(part, NumberStyles.Float, CultureInfo.InvariantCulture) * place;
            place *= 60;
        }

        return value;
    }

    private static int DigitValue(char character) => character switch
    {
        >= '0' and <= '9' => character - '0',
        >= 'a' and <= 'f' => character - 'a' + 10,
        >= 'A' and <= 'F' => character - 'A' + 10,
        _ => throw new ArgumentOutOfRangeException(nameof(character), character, "Dígito no válido."),
    };
}
