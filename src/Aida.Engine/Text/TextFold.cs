using System.Globalization;
using System.Text;

namespace Aida.Engine.Text;

/// <summary>Pliega un texto para compararlo: en minúsculas y sin tildes.</summary>
/// <remarks>
/// <para>Es <c>fold</c> de la 0.1 (<c>textmatch.py:38-48</c>), paso a paso: minúsculas, forma D y fuera las marcas que no
/// ocupan espacio. En el mostrador se teclea «gomez» tanto como «Gómez». La «ñ» queda en «n», como en la 0.1. No se
/// vuelve a componer en forma C, porque la 0.1 tampoco lo hace.</para>
/// <para>Vive en el motor y en un solo sitio porque lo comparten las búsquedas de los brazos y, cuando se porten, la
/// intención y los apartados: dos copias acabarían plegando distinto. El de <c>Aida.Guards</c> es de la herramienta de
/// guardianes y no lo usa ningún proyecto de producción.</para>
/// <para>Diferencias conocidas con la 0.1, que no aparecen en los nombres del dorado: <c>str.lower</c> de Python pone la
/// sigma final («ς») a final de palabra y convierte la «İ» en dos caracteres; <see cref="string.ToLowerInvariant"/> no
/// hace ninguna de las dos cosas. El resultado no depende de la cultura del equipo.</para>
/// </remarks>
public static class TextFold
{
    /// <summary>Pliega un texto.</summary>
    /// <param name="text">El texto.</param>
    /// <returns>El texto en minúsculas invariantes y en forma D, sin los caracteres <see cref="UnicodeCategory.NonSpacingMark"/>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> es <see langword="null"/>.</exception>
    public static string Fold(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
