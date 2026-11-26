namespace Aida.Platform.People;

/// <summary>La forma de un número de documento que se compara: sin separadores y en mayúsculas.</summary>
/// <remarks>«1.234.567», «1234567» y «1 234-567» son el mismo documento. La huella y el rechazo de un documento repetido
/// usan esta misma forma: si cada uno normalizara a su manera, un documento escrito con puntos no encontraría al que se
/// escribió sin ellos.</remarks>
public static class PersonDocument
{
    /// <summary>El número, sin espacios, puntos ni guiones y en mayúsculas invariantes.</summary>
    /// <param name="number">El número, tal como se escribió.</param>
    /// <returns>La forma que se compara; vacía si no tenía más que separadores.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="number"/> es <see langword="null"/>.</exception>
    public static string Normalize(string number)
    {
        ArgumentNullException.ThrowIfNull(number);
        return new string(number.Where(character => character is not (' ' or '.' or '-')).ToArray()).ToUpperInvariant();
    }
}
