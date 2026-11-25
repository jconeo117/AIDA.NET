using Aida.Engine.Text;
using Aida.Platform.Assistant;

namespace Aida.Cognition.Interpretation;

/// <summary>Lee los momentos de una frase: un día, una hora, una parte del día o varias de esas cosas.</summary>
/// <remarks>
/// <para>Recorre la frase de izquierda a derecha. En cada posición prueba las reglas en orden y la primera que encaja consume
/// sus palabras. Las palabras vacías no se saltan: cada regla dice cuáles admite.</para>
/// <para>Cada pieza se añade al último momento si a ese le falta, y abre uno nuevo si ya la tiene: «del martes al jueves a las
/// 11» son dos momentos, y «mañana en la mañana», uno. Una hora con su parte del día ocupan las dos piezas a la vez.</para>
/// </remarks>
internal sealed class WhenReader
{
    private readonly TimeVocabulary _vocabulary;

    /// <summary>Crea el lector con las palabras del tiempo del pack.</summary>
    /// <param name="vocabulary">El vocabulario.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="vocabulary"/> es <see langword="null"/>.</exception>
    internal WhenReader(TimeVocabulary vocabulary)
    {
        _vocabulary = vocabulary ?? throw new ArgumentNullException(nameof(vocabulary));
    }

    /// <summary>Lee los momentos de una frase.</summary>
    /// <param name="text">Lo que escribió el operador.</param>
    /// <param name="spans">Sus palabras.</param>
    /// <param name="today">El día de hoy, contra el que se resuelven las fechas.</param>
    /// <returns>Los momentos y las palabras que consumieron.</returns>
    internal WhenResult Read(string text, IReadOnlyList<TokenSpan> spans, DateOnly today)
    {
        var scan = new WhenScan(text, spans, _vocabulary, today);
        var mentions = new List<WhenMention>();
        var consumed = new List<int>();
        for (var at = 0; at < scan.Count; at++)
        {
            if (consumed.Contains(at))
            {
                continue;
            }

            if (ReadAt(scan, at, consumed) is { } mention)
            {
                Add(mentions, mention);
            }
        }

        return new WhenResult(mentions, consumed.ToHashSet());
    }

    // Las reglas en su orden: la primera que encaja gana. Cada una deja sus palabras en `consumed` solo si encaja.
    private static WhenMention? ReadAt(WhenScan scan, int at, List<int> consumed) =>
        DayExpressions.TryNumericDate(scan, at, consumed)
        ?? DayExpressions.TryRelative(scan, at, consumed)
        ?? DayExpressions.TryWeekday(scan, at, consumed)
        ?? DayExpressions.TryNamedMonth(scan, at, consumed)
        ?? DayExpressions.TryDayOfMonth(scan, at, consumed)
        ?? HourExpressions.TryHour(scan, at, consumed)
        ?? HourExpressions.TryPeriod(scan, at, consumed);

    private static void Add(List<WhenMention> mentions, WhenMention piece)
    {
        if (mentions.Count == 0 || !Fits(mentions[^1], piece))
        {
            mentions.Add(piece);
            return;
        }

        var last = mentions[^1];
        mentions[^1] = new WhenMention(
            last.Day ?? piece.Day,
            last.Time ?? piece.Time,
            last.Period != DayPeriod.Unknown ? last.Period : piece.Period);
    }

    // Una pieza entra en el último momento solo si ninguna de sus partes ya está en él.
    private static bool Fits(WhenMention last, WhenMention piece) =>
        (piece.Day is null || last.Day is null)
        && (piece.Time is null || last.Time is null)
        && (piece.Period == DayPeriod.Unknown || last.Period == DayPeriod.Unknown);
}
