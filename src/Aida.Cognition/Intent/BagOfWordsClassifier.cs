using Aida.Engine.Packs;
using Aida.Engine.Text;
using Aida.Platform.Assistant;

namespace Aida.Cognition.Intent;

/// <summary>Clasifica una frase contra un catálogo cerrado de intenciones, por bolsa de palabras y coseno.</summary>
/// <remarks>
/// Es <c>BagOfWordsClassifier</c> y <c>build_classifier</c> de la 0.1 (<c>intent/classify.py:33-83</c>): compara con cada
/// ejemplo por separado, se queda con el mejor y nunca inventa una intención que no esté en el catálogo. Con un pack de
/// motor se comporta como la 0.1; con el catálogo de un brazo, pliega y quita las palabras vacías que el brazo declara
/// (ADR 0037). Todavía no es el <c>IIntentClassifier</c> del bucle: la intención no empuja necesidades hasta el servicio
/// del bucle.
/// </remarks>
public sealed class BagOfWordsClassifier
{
    private readonly IReadOnlyList<(string Id, IReadOnlyDictionary<string, int> Example)> _references;
    private readonly double _threshold;
    private readonly IReadOnlySet<string>? _stopwords;
    private readonly bool _folded;

    private BagOfWordsClassifier(
        IReadOnlyList<(string Id, IReadOnlyDictionary<string, int> Example)> references,
        double threshold,
        IReadOnlySet<string>? stopwords,
        bool folded)
    {
        _references = references;
        _threshold = threshold;
        _stopwords = stopwords;
        _folded = folded;
    }

    /// <summary>El clasificador de un pack de motor, como en la 0.1: sin palabras vacías y sin plegar.</summary>
    /// <param name="pack">El pack.</param>
    /// <returns>El clasificador con el umbral de <c>pack.Tuning</c>, o <see langword="null"/> si el pack no declara
    /// intenciones (<c>classify.py:71-75</c>).</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="pack"/> es <see langword="null"/>.</exception>
    /// <remarks>Las referencias van ordenadas por id en orden ordinal, y dentro de cada id, los ejemplos en el orden del pack.
    /// Así el desempate del clasificador no depende de cómo se declaró el pack (<c>classify.py:78</c>).</remarks>
    public static BagOfWordsClassifier? FromPack(ContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (pack.Intents.IsEmpty)
        {
            return null;
        }

        var references = pack.Intents
            .OrderBy(intent => intent.Id.Value, StringComparer.Ordinal)
            .SelectMany(intent => intent.Examples.Select(example => (Id: intent.Id.Value, Example: WordBag.Vectorize(example))))
            .ToList();
        return new BagOfWordsClassifier(references, pack.Tuning.IntentConfidenceThreshold, null, folded: false);
    }

    /// <summary>El clasificador del catálogo de un brazo: pliega el texto y quita sus palabras vacías.</summary>
    /// <param name="catalog">El catálogo.</param>
    /// <returns>El clasificador.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="catalog"/> es <see langword="null"/>.</exception>
    public static BagOfWordsClassifier FromCatalog(IntentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var references = catalog.Intents
            .OrderBy(intent => intent.Id, StringComparer.Ordinal)
            .SelectMany(intent => intent.Examples.Select(example => (Id: intent.Id, Example: WordBag.Vectorize(TextFold.Fold(example), catalog.Stopwords))))
            .ToList();
        return new BagOfWordsClassifier(references, catalog.Threshold, catalog.Stopwords, folded: true);
    }

    /// <summary>La intención de una frase.</summary>
    /// <param name="utterance">Lo que se escribió.</param>
    /// <returns>El id, o <see langword="null"/> si la frase no tiene palabras (una vez quitadas las vacías) o nada
    /// alcanza el umbral.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="utterance"/> es <see langword="null"/>.</exception>
    /// <remarks>La comparación es estricta: en un empate se queda la primera referencia vista, la de id menor
    /// (<c>classify.py:63</c>). Con <c>>=</c> ganaría la última, y el resultado dependería del orden de los ejemplos.</remarks>
    public string? Classify(string utterance)
    {
        ArgumentNullException.ThrowIfNull(utterance);
        var query = WordBag.Vectorize(_folded ? TextFold.Fold(utterance) : utterance, _stopwords);
        if (query.Count == 0)
        {
            return null;
        }

        var bestScore = 0.0;
        string? bestId = null;
        foreach (var (id, example) in _references)
        {
            var score = WordBag.Cosine(query, example);
            if (score > bestScore)
            {
                bestScore = score;
                bestId = id;
            }
        }

        return bestId is null || bestScore < _threshold ? null : bestId;
    }
}
