using System.Text.RegularExpressions;

namespace Aida.Engine.Text;

/// <summary>Las palabras de un texto: la única puerta para partirlo.</summary>
/// <remarks>Es <c>token_spans</c> y <c>tokenize</c> de la 0.1 (<c>textmatch.py:18-35</c>): el mismo patrón, en minúsculas,
/// sin raíces ni sinónimos. Vive en el motor porque lo comparten la intención y, cuando se porten, los demás brazos:
/// dos patrones acabarían partiendo distinto.</remarks>
public static partial class TextTokens
{
    /// <summary>Cada palabra con su posición.</summary>
    /// <param name="text">El texto.</param>
    /// <returns>Las palabras, en el orden del texto.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> es <see langword="null"/>.</exception>
    public static IReadOnlyList<TokenSpan> Spans(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. WordPattern().Matches(text).Select(match => new TokenSpan(match.Value.ToLowerInvariant(), match.Index, match.Index + match.Length))];
    }

    /// <summary>Solo las palabras.</summary>
    /// <param name="text">El texto.</param>
    /// <returns>Las palabras, en el orden del texto.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> es <see langword="null"/>.</exception>
    public static IReadOnlyList<string> Tokenize(string text) => [.. Spans(text).Select(span => span.Token)];

    [GeneratedRegex("[a-zA-Z0-9áéíóúüñÁÉÍÓÚÜÑ]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();
}
