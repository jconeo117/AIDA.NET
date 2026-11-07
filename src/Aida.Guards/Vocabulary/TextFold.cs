using System.Globalization;
using System.Text;

namespace Aida.Guards.Vocabulary;

/// <summary>Pliega un texto para comparar palabras.</summary>
/// <remarks>Sin tildes y en minúsculas, para que «Diagnóstico» y «DIAGNOSTICO» sean una sola entrada de la lista.</remarks>
public static class TextFold
{
    /// <summary>Pliega un texto.</summary>
    /// <param name="text">El texto.</param>
    /// <returns>El texto en forma D, sin las marcas <c>NonSpacingMark</c>, en forma C y con <c>ToLowerInvariant</c>. La «ñ» queda en «n».</returns>
    /// <remarks>Invariante: el resultado no depende de la cultura del equipo.</remarks>
    public static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
