using Aida.Cognition.Intent;
using Aida.Engine.Text;
using Aida.Platform.Assistant;

namespace Aida.Cognition.Understanding;

/// <summary>Entiende una frase del operador: la intención, el teléfono y el papel de cada palabra.</summary>
/// <remarks>
/// No decide ni cambia nada: devuelve lo entendido, y el brazo propone (ADR 0037). Es la pieza que un modelo de lenguaje
/// sustituirá o completará más adelante; sin él, AIDA sigue entendiendo así. Los momentos (día y hora) llegan en N013.
/// </remarks>
public sealed class AssistantInterpreter
{
    private const string PhoneSeparators = " -.";
    private const int MinPhoneDigits = 7;
    private const int MaxPhoneDigits = 15;

    private readonly IntentCatalog _catalog;
    private readonly TimeVocabulary _vocabulary;
    private readonly BagOfWordsClassifier _classifier;
    private readonly IReadOnlySet<string> _intentWords;

    /// <summary>Crea el intérprete de un brazo.</summary>
    /// <param name="catalog">Las intenciones del brazo.</param>
    /// <param name="vocabulary">Las palabras del tiempo.</param>
    /// <exception cref="ArgumentNullException">Si alguno es <see langword="null"/>.</exception>
    /// <remarks>El clasificador y las palabras de los ejemplos se calculan una vez, aquí, y no en cada frase.</remarks>
    public AssistantInterpreter(IntentCatalog catalog, TimeVocabulary vocabulary)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _vocabulary = vocabulary ?? throw new ArgumentNullException(nameof(vocabulary));
        _classifier = BagOfWordsClassifier.FromCatalog(catalog);
        _intentWords = catalog.Intents
            .SelectMany(intent => intent.Examples)
            .SelectMany(example => TextTokens.Tokenize(example))
            .Select(TextFold.Fold)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Entiende una frase.</summary>
    /// <param name="text">Lo que escribió el operador.</param>
    /// <returns>Lo entendido. Con un texto vacío o de solo espacios, sin intención, sin teléfono y sin palabras.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> es <see langword="null"/>.</exception>
    /// <remarks>El papel de cada palabra sale en un orden fijo: teléfono, número, tiempo, vacía, intención y, si nada de eso,
    /// libre. Un nombre se queda libre porque no está en ninguna lista, no porque se adivine.</remarks>
    public Aida.Platform.Assistant.Understanding Interpret(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var spans = TextTokens.Spans(text);
        var phone = FirstPhoneRun(text, spans);
        var words = new List<Word>(spans.Count);
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            var role = phone is { } run && index >= run.From && index <= run.To
                ? WordRole.Phone
                : RoleOf(span.Token, TextFold.Fold(span.Token));
            words.Add(new Word(text[span.Start..span.End], TextFold.Fold(span.Token), role));
        }

        var digits = phone is { } found
            ? string.Concat(spans.Skip(found.From).Take(found.To - found.From + 1).Select(span => span.Token))
            : null;
        return new Aida.Platform.Assistant.Understanding(_classifier.Classify(text), digits, words, []);
    }

    private WordRole RoleOf(string token, string folded)
    {
        if (IsDigits(token))
        {
            return WordRole.Number;
        }

        if (_vocabulary.Words.Contains(folded))
        {
            return WordRole.Time;
        }

        if (_catalog.Stopwords.Contains(folded))
        {
            return WordRole.Filler;
        }

        return _intentWords.Contains(folded) ? WordRole.Intent : WordRole.Free;
    }

    // La primera racha de palabras numéricas, seguidas y separadas solo por espacios, guiones o puntos, que suma de 7 a 15
    // dígitos. Una racha más corta o más larga no es teléfono: «300-55» son dos números.
    private static (int From, int To)? FirstPhoneRun(string text, IReadOnlyList<TokenSpan> spans)
    {
        for (var from = 0; from < spans.Count; from++)
        {
            if (!IsDigits(spans[from].Token))
            {
                continue;
            }

            var to = from;
            var digits = spans[from].Token.Length;
            while (to + 1 < spans.Count
                && IsDigits(spans[to + 1].Token)
                && OnlySeparators(text, spans[to].End, spans[to + 1].Start))
            {
                to++;
                digits += spans[to].Token.Length;
            }

            if (digits is >= MinPhoneDigits and <= MaxPhoneDigits)
            {
                return (from, to);
            }

            from = to;
        }

        return null;
    }

    private static bool OnlySeparators(string text, int from, int to) =>
        text.AsSpan(from, to - from).IndexOfAnyExcept(PhoneSeparators.AsSpan()) < 0;

    private static bool IsDigits(string token) => token.Length > 0 && token.All(character => character is >= '0' and <= '9');
}
