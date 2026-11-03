using System.Reflection;
using System.Text;

namespace Aida.Guards.Vocabulary;

/// <summary>Carga las listas de términos prohibidos.</summary>
/// <remarks>
/// Las listas son datos, en <c>Vocabulary/*.txt</c> como recursos incrustados: crecen con cada brazo
/// (ADR 0002) sin tocar el código del guardián.
/// </remarks>
public static class ForbiddenTermCatalog
{
    /// <summary>Lee una lista.</summary>
    /// <param name="content">Un término por línea. Se ignoran las líneas vacías y las que empiezan por <c>#</c>.</param>
    /// <param name="scope">El alcance de todos los términos de la lista.</param>
    /// <returns>Los términos, con las palabras de <see cref="WordSplitter.Split"/>, en el orden del archivo y sin repetidos (dos términos son iguales si sus palabras lo son).</returns>
    /// <remarks>Recibe el contenido y no una ruta, para probar sin archivos.</remarks>
    public static IReadOnlyList<ForbiddenTerm> Parse(string content, TermScope scope)
    {
        var terms = new List<ForbiddenTerm>();
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var words = WordSplitter.Split(line);
            if (words.Count > 0 && !terms.Any(existing => SameWords(existing.Words, words)))
            {
                terms.Add(new ForbiddenTerm(words, scope));
            }
        }

        return terms;
    }

    /// <summary>Carga las dos listas del repositorio.</summary>
    /// <returns>Los términos de <c>forbidden-in-code.txt</c> con <see cref="TermScope.Code"/>, seguidos de los de <c>forbidden-everywhere.txt</c> con <see cref="TermScope.Everywhere"/>.</returns>
    /// <exception cref="InvalidOperationException">Si falta un recurso o si un término está en las dos listas.</exception>
    /// <remarks>Falla en lugar de degradar. En Python (I-009), un pack que no parseaba dejaba el guardián con una lista vieja, sin avisar.</remarks>
    public static IReadOnlyList<ForbiddenTerm> LoadDefault()
    {
        var code = Parse(ReadResource("Aida.Guards.Vocabulary.forbidden-in-code.txt"), TermScope.Code);
        var everywhere = Parse(ReadResource("Aida.Guards.Vocabulary.forbidden-everywhere.txt"), TermScope.Everywhere);
        foreach (var term in everywhere)
        {
            if (code.Any(other => SameWords(other.Words, term.Words)))
            {
                throw new InvalidOperationException($"El término «{string.Join(" ", term.Words)}» está en las dos listas de vocabulario; déjalo en una sola.");
            }
        }

        return [.. code, .. everywhere];
    }

    private static bool SameWords(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.SequenceEqual(right, StringComparer.Ordinal);

    private static string ReadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Falta el recurso incrustado «{name}».");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
