namespace Aida.Engine.Text;

/// <summary>Una bolsa de palabras y el coseno entre dos: la única aritmética de «esto se parece a esto».</summary>
/// <remarks>Es <c>vectorize</c>, <c>_norm</c> y <c>cosine</c> de la 0.1 (<c>textmatch.py:50-80</c>), bit a bit: las sumas
/// recorren las claves en orden ordinal, los cuadrados y el producto son enteros, y solo la raíz y la división son de
/// coma flotante, como en Python. Las palabras vacías llegan vacías por defecto, como en la 0.1: quien las quiera las
/// declara en sus datos.</remarks>
public static class WordBag
{
    /// <summary>El recuento de palabras de un texto, sin las palabras vacías.</summary>
    /// <param name="text">El texto.</param>
    /// <param name="stopwords">Las palabras que no cuentan, o <see langword="null"/> para ninguna.</param>
    /// <returns>Cada palabra con cuántas veces aparece. Vacío si no hay ninguna.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> es <see langword="null"/>.</exception>
    /// <remarks>Las palabras vacías se comparan ya plegadas por quien las declara: aquí se comparan tal cual, como en la 0.1.</remarks>
    public static IReadOnlyDictionary<string, int> Vectorize(string text, IReadOnlySet<string>? stopwords = null)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var token in TextTokens.Tokenize(text))
        {
            if (stopwords is not null && stopwords.Contains(token))
            {
                continue;
            }

            counts[token] = counts.GetValueOrDefault(token) + 1;
        }

        return counts;
    }

    /// <summary>El coseno entre dos recuentos.</summary>
    /// <param name="a">Un recuento.</param>
    /// <param name="b">El otro.</param>
    /// <returns>Entre 0 y 1; 0 si no comparten ninguna palabra o alguno está vacío.</returns>
    /// <exception cref="ArgumentNullException">Si alguno es <see langword="null"/>.</exception>
    /// <remarks>La suma del producto y de las normas va en un <see cref="long"/>, no en coma flotante: así el resultado no
    /// depende del orden de las sumas, y es el de Python bit a bit.</remarks>
    public static double Cosine(IReadOnlyDictionary<string, int> a, IReadOnlyDictionary<string, int> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var shared = a.Keys.Where(b.ContainsKey).Order(StringComparer.Ordinal).ToList();
        if (shared.Count == 0)
        {
            return 0.0;
        }

        long dot = 0;
        foreach (var key in shared)
        {
            dot += (long)a[key] * b[key];
        }

        var normA = Norm(a);
        var normB = Norm(b);
        return normA == 0.0 || normB == 0.0 ? 0.0 : dot / (normA * normB);
    }

    private static double Norm(IReadOnlyDictionary<string, int> vector)
    {
        long sum = 0;
        foreach (var key in vector.Keys.Order(StringComparer.Ordinal))
        {
            sum += (long)vector[key] * vector[key];
        }

        return Math.Sqrt(sum);
    }
}
