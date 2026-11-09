using System.Globalization;
using System.Text.RegularExpressions;
using Aida.Engine.Ontology;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Convierte un escalar al tipo de un campo, como lo hace pydantic en la 0.1 (§A.8).</summary>
/// <remarks>
/// pydantic es tolerante: acepta <c>"0.5"</c> entre comillas en un campo decimal, y <c>true</c> como 1.0. Lo que
/// la tabla no acepta es un problema de campo, con su ruta. El mensaje es propio de .NET: solo se compara la ruta.
/// </remarks>
internal static class FieldReader
{
    private static readonly Regex _decimalText = new(
        @"^[+-]?(?:[0-9](?:_?[0-9])*(?:\.(?:[0-9](?:_?[0-9])*)?)?|\.[0-9](?:_?[0-9])*)(?:[eE][+-]?[0-9](?:_?[0-9])*)?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex _specialDecimalText = new(
        "^[+-]?(?:inf|infinity|nan)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex _integerText = new("^[+-]?[0-9](?:_?[0-9])*$", RegexOptions.CultureInvariant);

    /// <summary>Lee un nodo que debe ser un mapa.</summary>
    /// <param name="node">El nodo.</param>
    /// <param name="path">La ruta del nodo.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    /// <param name="mapping">El mapa, si lo es.</param>
    /// <returns><see langword="true"/> si el nodo es un mapa.</returns>
    internal static bool TryMapping(YamlNode node, string path, ProblemList problems, out YamlMappingNode mapping)
    {
        if (node is YamlMappingNode found)
        {
            mapping = found;
            return true;
        }

        mapping = null!;
        problems.Add(path, "Input should be a valid dictionary");
        return false;
    }

    /// <summary>Lee un texto. Solo acepta escalares que PyYAML no resuelve como número, booleano o nulo.</summary>
    internal static bool TryText(YamlNode node, string path, ProblemList problems, out string value)
    {
        if (node is YamlScalarNode scalar && YamlScalar.KindOf(scalar) == ScalarKind.Text)
        {
            value = YamlScalar.TextOf(scalar);
            return true;
        }

        value = string.Empty;
        problems.Add(path, "Input should be a valid string");
        return false;
    }

    /// <summary>Lee un decimal: un entero, un decimal, un booleano (1.0 o 0.0) o un texto numérico.</summary>
    internal static bool TryFloat(YamlNode node, string path, ProblemList problems, out double value)
    {
        value = 0;
        if (node is YamlScalarNode scalar)
        {
            var text = YamlScalar.TextOf(scalar);
            switch (YamlScalar.KindOf(scalar))
            {
                case ScalarKind.Float:
                    value = YamlScalar.FloatOf(text);
                    return true;
                case ScalarKind.Int:
                    if (YamlScalar.TryIntOf(text, out var integer))
                    {
                        value = integer;
                        return true;
                    }

                    return false;
                case ScalarKind.Bool:
                    value = YamlScalar.BoolOf(text) ? 1.0 : 0.0;
                    return true;
                case ScalarKind.Text:
                    return TryFloatText(text.Trim(), path, problems, out value);
            }
        }

        problems.Add(path, "Input should be a valid number");
        return false;
    }

    /// <summary>Lee un entero: un entero, un decimal con valor entero, un booleano o un texto con dígitos.</summary>
    internal static bool TryInt(YamlNode node, string path, ProblemList problems, out long value)
    {
        value = 0;
        if (node is YamlScalarNode scalar)
        {
            var text = YamlScalar.TextOf(scalar);
            switch (YamlScalar.KindOf(scalar))
            {
                case ScalarKind.Int:
                    return YamlScalar.TryIntOf(text, out value);
                case ScalarKind.Float:
                    if (TryWholeDouble(YamlScalar.FloatOf(text), out value))
                    {
                        return true;
                    }

                    break;
                case ScalarKind.Bool:
                    value = YamlScalar.BoolOf(text) ? 1 : 0;
                    return true;
                case ScalarKind.Text:
                    if (TryIntegerText(text.Trim(), out value))
                    {
                        return true;
                    }

                    break;
            }
        }

        problems.Add(path, "Input should be a valid integer");
        return false;
    }

    /// <summary>Lee un booleano: un booleano, un 0 o un 1, o uno de los textos de pydantic.</summary>
    internal static bool TryBool(YamlNode node, string path, ProblemList problems, out bool value)
    {
        value = false;
        if (node is YamlScalarNode scalar)
        {
            var text = YamlScalar.TextOf(scalar);
            switch (YamlScalar.KindOf(scalar))
            {
                case ScalarKind.Bool:
                    value = YamlScalar.BoolOf(text);
                    return true;
                case ScalarKind.Int when YamlScalar.TryIntOf(text, out var integer) && integer is 0 or 1:
                    value = integer == 1;
                    return true;
                case ScalarKind.Text:
                    var word = text.ToLowerInvariant() switch
                    {
                        "1" or "on" or "t" or "true" or "y" or "yes" => true,
                        "0" or "off" or "f" or "false" or "n" or "no" => false,
                        _ => (bool?)null,
                    };
                    if (word is { } known)
                    {
                        value = known;
                        return true;
                    }

                    break;
            }
        }

        problems.Add(path, "Input should be a valid boolean");
        return false;
    }

    /// <summary>Lee el nombre de una reversibilidad en el YAML.</summary>
    internal static bool TryReversibility(YamlNode node, string path, ProblemList problems, out Reversibility value) =>
        TryEnumName(node, path, problems, (string text, out Reversibility found) => OntologyNames.TryParse(text, out found), out value);

    /// <summary>Lee el nombre de una perilla del temperamento en el YAML.</summary>
    internal static bool TryTemperamentKnob(YamlNode node, string path, ProblemList problems, out TemperamentKnob value) =>
        TryEnumName(node, path, problems, (string text, out TemperamentKnob found) => OntologyNames.TryParse(text, out found), out value);

    /// <summary>Aplica una restricción numérica a un conversor. Un NaN no la cumple, como en pydantic.</summary>
    /// <typeparam name="T">El tipo numérico.</typeparam>
    /// <param name="inner">El conversor.</param>
    /// <param name="ok">La restricción.</param>
    /// <param name="message">El mensaje si no se cumple.</param>
    /// <returns>Un conversor que además comprueba la restricción.</returns>
    internal static NodeReader<T> Constrained<T>(NodeReader<T> inner, Func<T, bool> ok, string message) =>
        (YamlNode node, string path, ProblemList problems, out T value) =>
        {
            if (!inner(node, path, problems, out value))
            {
                return false;
            }

            if (ok(value))
            {
                return true;
            }

            problems.Add(path, message);
            return false;
        };

    /// <summary>Un conversor para un campo <c>X?</c>: el nulo vale <see langword="null"/>.</summary>
    /// <typeparam name="T">El tipo de <c>X</c>.</typeparam>
    /// <param name="inner">El conversor de <c>X</c>.</param>
    /// <returns>Un conversor de <c>X?</c>.</returns>
    internal static NodeReader<T?> Optional<T>(NodeReader<T> inner) where T : struct =>
        (YamlNode node, string path, ProblemList problems, out T? value) =>
        {
            if (IsNull(node))
            {
                value = null;
                return true;
            }

            if (inner(node, path, problems, out var converted))
            {
                value = converted;
                return true;
            }

            value = null;
            return false;
        };

    /// <summary>Un conversor de identificadores: un texto, envuelto en su tipo.</summary>
    /// <typeparam name="T">El tipo del identificador.</typeparam>
    /// <param name="make">Cómo se envuelve el texto.</param>
    /// <returns>Un conversor del identificador.</returns>
    internal static NodeReader<T> Id<T>(Func<string, T> make) =>
        (YamlNode node, string path, ProblemList problems, out T value) =>
        {
            if (TryText(node, path, problems, out var text))
            {
                value = make(text);
                return true;
            }

            value = default!;
            return false;
        };

    /// <summary>Indica si un nodo es el nulo.</summary>
    internal static bool IsNull(YamlNode node) =>
        node is YamlScalarNode scalar && YamlScalar.KindOf(scalar) == ScalarKind.Null;

    // Un nombre que no es texto ya tiene su problema de tipo; uno que es texto y no existe, el de enumerado.
    private static bool TryEnumName<T>(YamlNode node, string path, ProblemList problems, TryParser<T> parse, out T value)
    {
        value = default!;
        if (!TryText(node, path, problems, out var text))
        {
            return false;
        }

        if (parse(text, out value))
        {
            return true;
        }

        problems.Add(path, "Input should be a valid enumeration member");
        return false;
    }

    private delegate bool TryParser<T>(string text, out T value);

    private static bool TryFloatText(string text, string path, ProblemList problems, out double value)
    {
        value = 0;
        if (_specialDecimalText.IsMatch(text))
        {
            var negative = text.StartsWith('-');
            var name = text.TrimStart('+', '-').ToLowerInvariant();
            value = name == "nan" ? double.NaN : negative ? double.NegativeInfinity : double.PositiveInfinity;
            return true;
        }

        if (_decimalText.IsMatch(text))
        {
            value = double.Parse(text.Replace("_", string.Empty, StringComparison.Ordinal), NumberStyles.Float, CultureInfo.InvariantCulture);
            return true;
        }

        problems.Add(path, "Input should be a valid number, unable to parse string as a number");
        return false;
    }

    private static bool TryIntegerText(string text, out long value)
    {
        value = 0;
        return _integerText.IsMatch(text)
            && long.TryParse(text.Replace("_", string.Empty, StringComparison.Ordinal), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryWholeDouble(double number, out long value)
    {
        value = 0;
        if (!double.IsFinite(number) || Math.Floor(number) != number || Math.Abs(number) >= 9.2233720368547758E18)
        {
            return false;
        }

        value = (long)number;
        return true;
    }
}
