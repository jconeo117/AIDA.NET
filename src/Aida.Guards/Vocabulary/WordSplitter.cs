using System.Text;

namespace Aida.Guards.Vocabulary;

/// <summary>Parte un texto en palabras plegadas.</summary>
/// <remarks>
/// Un solo algoritmo para identificadores, cadenas y comentarios. En C#, los identificadores van en
/// PascalCase; en Python iban en snake_case, y allí bastaba con cortar por <c>_</c>.
/// </remarks>
public static class WordSplitter
{
    /// <summary>Parte en palabras.</summary>
    /// <param name="text">Identificador o texto libre.</param>
    /// <returns>Las palabras, plegadas y en su orden, sin vacías.</returns>
    /// <remarks>
    /// Se corta: en todo carácter que no es letra ni dígito, que se descarta; entre minúscula y mayúscula
    /// (<c>patientRecord</c>); entre letra y dígito, en los dos sentidos (<c>Cie10</c>); y antes de la última
    /// mayúscula de una racha seguida de minúscula (<c>HTTPServer</c> da <c>http</c> y <c>server</c>). Después
    /// se pliega cada palabra.
    /// </remarks>
    public static IReadOnlyList<string> Split(string text)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (!char.IsLetterOrDigit(character))
            {
                Flush(current, words);
                continue;
            }

            if (current.Length > 0 && StartsWord(text, index))
            {
                Flush(current, words);
            }

            current.Append(character);
        }

        Flush(current, words);
        return words;
    }

    // Llamado solo con una palabra abierta: el carácter anterior de `text` es letra o dígito.
    private static bool StartsWord(string text, int index)
    {
        var previous = text[index - 1];
        var character = text[index];
        if (char.IsLower(previous) && char.IsUpper(character))
        {
            return true;
        }

        if ((char.IsLetter(previous) && char.IsDigit(character)) || (char.IsDigit(previous) && char.IsLetter(character)))
        {
            return true;
        }

        return char.IsUpper(previous)
            && char.IsUpper(character)
            && index + 1 < text.Length
            && char.IsLower(text[index + 1]);
    }

    private static void Flush(StringBuilder current, List<string> words)
    {
        if (current.Length == 0)
        {
            return;
        }

        words.Add(TextFold.Fold(current.ToString()));
        current.Clear();
    }
}
