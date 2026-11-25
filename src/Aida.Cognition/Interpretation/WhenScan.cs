using System.Globalization;
using Aida.Engine.Text;
using Aida.Platform.Assistant;

namespace Aida.Cognition.Interpretation;

/// <summary>Lo que las reglas de fechas y horas necesitan de una frase: sus palabras, el texto original y el reloj.</summary>
/// <remarks>
/// Las palabras van plegadas para comparar, pero el texto original sigue disponible: una regla que mira el carácter que hay
/// entre dos números («300-555» contra «300 555») necesita lo que se escribió, no la bolsa de palabras.
/// </remarks>
internal sealed class WhenScan
{
    /// <summary>Crea el recorrido de una frase.</summary>
    /// <param name="text">Lo que escribió el operador.</param>
    /// <param name="spans">Sus palabras, con su posición.</param>
    /// <param name="vocabulary">Las palabras del tiempo del pack.</param>
    /// <param name="today">El día de hoy.</param>
    internal WhenScan(string text, IReadOnlyList<TokenSpan> spans, TimeVocabulary vocabulary, DateOnly today)
    {
        Text = text;
        Spans = spans;
        Vocabulary = vocabulary;
        Today = today;
        Folded = spans.Select(span => TextFold.Fold(span.Token)).ToList();
    }

    /// <summary>El texto original.</summary>
    internal string Text { get; }

    /// <summary>Las palabras, en orden.</summary>
    internal IReadOnlyList<TokenSpan> Spans { get; }

    /// <summary>Las palabras plegadas, en el mismo orden que <see cref="Spans"/>.</summary>
    internal IReadOnlyList<string> Folded { get; }

    /// <summary>Las palabras del tiempo.</summary>
    internal TimeVocabulary Vocabulary { get; }

    /// <summary>El día de hoy.</summary>
    internal DateOnly Today { get; }

    /// <summary>Cuántas palabras tiene la frase.</summary>
    internal int Count => Spans.Count;

    /// <summary>La palabra plegada de una posición, o vacía fuera de la frase.</summary>
    /// <param name="index">La posición.</param>
    /// <returns>La palabra, o <see cref="string.Empty"/> si la posición no existe.</returns>
    internal string Word(int index) => index >= 0 && index < Count ? Folded[index] : string.Empty;

    /// <summary>Si la palabra de una posición está en un conjunto.</summary>
    /// <param name="index">La posición.</param>
    /// <param name="words">El conjunto de palabras plegadas.</param>
    /// <returns><see langword="true"/> si está.</returns>
    internal bool IsWord(int index, IReadOnlySet<string> words) => words.Contains(Word(index));

    /// <summary>El número de una posición, si la palabra son solo dígitos y cabe en un entero.</summary>
    /// <param name="index">La posición.</param>
    /// <returns>El número, o <see langword="null"/>.</returns>
    internal int? Number(int index)
    {
        if (index < 0 || index >= Count || !IsDigits(Spans[index].Token))
        {
            return null;
        }

        return int.TryParse(Spans[index].Token, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>Lo que se escribió entre una palabra y la siguiente.</summary>
    /// <param name="index">La primera de las dos; debe existir la siguiente.</param>
    /// <returns>El texto entre ambas, tal como está en el original.</returns>
    internal string Gap(int index) => Text[Spans[index].End..Spans[index + 1].Start];

    /// <summary>Si la palabra de una posición y la siguiente solo las separa ese carácter.</summary>
    /// <param name="index">La primera de las dos.</param>
    /// <param name="separator">El separador exacto, como <c>/</c> o <c>:</c>.</param>
    /// <returns><see langword="true"/> si las dos están juntas y separadas solo por él.</returns>
    internal bool HasGap(int index, string separator) => index >= 0 && index + 1 < Count && Gap(index) == separator;

    /// <summary>Si una palabra son solo dígitos ASCII.</summary>
    /// <param name="token">La palabra.</param>
    /// <returns><see langword="true"/> si lo son y no está vacía.</returns>
    internal static bool IsDigits(string token) => token.Length > 0 && token.All(character => character is >= '0' and <= '9');
}
