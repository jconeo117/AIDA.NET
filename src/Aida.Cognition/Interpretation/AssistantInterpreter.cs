using Aida.Cognition.Intent;
using Aida.Engine.Text;
using Aida.Platform.Assistant;

namespace Aida.Cognition.Interpretation;

/// <summary>Entiende una frase del operador: la intención, el teléfono, los momentos y el papel de cada palabra.</summary>
/// <remarks>
/// No decide ni cambia nada: devuelve lo entendido, y el brazo propone (ADR 0037). Es la pieza que un modelo de lenguaje
/// sustituirá o completará más adelante; sin él, AIDA sigue entendiendo así.
/// </remarks>
public sealed class AssistantInterpreter
{
    private const string PhoneSeparators = " -.";
    private const int MinPhoneDigits = 7;
    private const int MaxPhoneDigits = 15;

    private readonly IntentCatalog _catalog;
    private readonly TimeVocabulary _vocabulary;
    private readonly BagOfWordsClassifier _classifier;
    private readonly WhenReader _when;
    private readonly IReadOnlySet<string> _intentWords;

    /// <summary>Crea el intérprete de un brazo.</summary>
    /// <param name="catalog">Las intenciones del brazo.</param>
    /// <param name="vocabulary">Las palabras del tiempo.</param>
    /// <exception cref="ArgumentNullException">Si alguno es <see langword="null"/>.</exception>
    /// <remarks>El clasificador, el lector de fechas y las palabras de los ejemplos se calculan una vez, aquí, y no en cada frase.</remarks>
    public AssistantInterpreter(IntentCatalog catalog, TimeVocabulary vocabulary)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _vocabulary = vocabulary ?? throw new ArgumentNullException(nameof(vocabulary));
        _classifier = BagOfWordsClassifier.FromCatalog(catalog);
        _when = new WhenReader(vocabulary);
        _intentWords = catalog.Intents
            .SelectMany(intent => intent.Examples)
            .SelectMany(example => TextTokens.Tokenize(example))
            .Select(TextFold.Fold)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Entiende una frase.</summary>
    /// <param name="text">Lo que escribió el operador.</param>
    /// <param name="today">El día de hoy, el del reloj de quien llama: contra él se resuelven «hoy», «el jueves» o «20/11».</param>
    /// <returns>Lo entendido. Con un texto vacío o de solo espacios, sin intención, sin teléfono, sin palabras y sin momentos.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> es <see langword="null"/>.</exception>
    /// <remarks>Los momentos se leen antes que el teléfono: las palabras que forman un momento no pueden ser también un teléfono
    /// («hoy 10» no es un número de teléfono).</remarks>
    public Understanding Interpret(string text, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(text);
        var spans = TextTokens.Spans(text);
        var when = _when.Read(text, spans, today);
        var phone = FirstPhoneRun(text, spans, when.Consumed);
        var words = new List<Word>(spans.Count);
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            var folded = TextFold.Fold(span.Token);
            var role = when.Consumed.Contains(index)
                ? WordRole.Time
                : phone is { } run && index >= run.From && index <= run.To
                    ? WordRole.Phone
                    : RoleOf(span.Token, folded);
            words.Add(new Word(text[span.Start..span.End], folded, role));
        }

        var digits = phone is { } found
            ? string.Concat(spans.Skip(found.From).Take(found.To - found.From + 1).Select(span => span.Token))
            : null;
        return new Understanding(_classifier.Classify(text), digits, words, when.Mentions);
    }

    // Las palabras que no son de un momento ni de un teléfono: número, tiempo, vacía, intención o libre.
    private WordRole RoleOf(string token, string folded)
    {
        if (WhenScan.IsDigits(token))
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
    // dígitos. Las que ya forman un momento no cuentan. Una racha más corta o más larga no es teléfono.
    private static (int From, int To)? FirstPhoneRun(string text, IReadOnlyList<TokenSpan> spans, IReadOnlySet<int> consumed)
    {
        for (var from = 0; from < spans.Count; from++)
        {
            if (!IsPhoneDigit(spans, from, consumed))
            {
                continue;
            }

            var to = from;
            var digits = spans[from].Token.Length;
            while (to + 1 < spans.Count
                && IsPhoneDigit(spans, to + 1, consumed)
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

    private static bool IsPhoneDigit(IReadOnlyList<TokenSpan> spans, int index, IReadOnlySet<int> consumed) =>
        !consumed.Contains(index) && WhenScan.IsDigits(spans[index].Token);

    private static bool OnlySeparators(string text, int from, int to) =>
        text.AsSpan(from, to - from).IndexOfAnyExcept(PhoneSeparators.AsSpan()) < 0;
}
