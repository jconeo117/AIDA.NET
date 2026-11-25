using Aida.Platform.Assistant;

namespace Aida.Cognition.Interpretation;

/// <summary>Las reglas de hora: una hora con su marcador, con sus minutos o con su parte del día, y una parte del día sola.</summary>
/// <remarks>
/// <para>Un número suelto no es una hora («llame al 300 555 1234» no tiene ninguna): hace falta un marcador («las 10»), una
/// forma con minutos («10:30», «10 y media», «10 y cuarto») o una parte del día («3 de la noche»).</para>
/// <para>La parte del día aclara la hora: con Afternoon, una hora menor de 12 suma 12; con Morning, las 12 son las 0.</para>
/// </remarks>
internal static class HourExpressions
{
    /// <summary>La regla 5: una hora, o una palabra de mediodía.</summary>
    /// <param name="scan">La frase.</param>
    /// <param name="at">La posición de la primera palabra de la hora.</param>
    /// <param name="consumed">Las posiciones usadas, a las que se añaden las de esta regla si encaja.</param>
    /// <returns>El momento, o <see langword="null"/>.</returns>
    internal static WhenMention? TryHour(WhenScan scan, int at, List<int> consumed)
    {
        if (scan.IsWord(at, scan.Vocabulary.Noon))
        {
            consumed.Add(at);
            return new WhenMention(null, new TimeOnly(12, 0), DayPeriod.Unknown);
        }

        var used = new List<int>();
        var index = at;
        if (scan.IsWord(index, scan.Vocabulary.HourLinks) && scan.IsWord(index + 1, scan.Vocabulary.HourMarkers))
        {
            used.Add(index);
            index++;
        }

        var marked = scan.IsWord(index, scan.Vocabulary.HourMarkers);
        if (marked)
        {
            used.Add(index);
            index++;
        }

        if (scan.Number(index) is not { } hour || hour > 23)
        {
            return null;
        }

        used.Add(index);
        var next = index + 1;
        var minute = 0;
        var hasMinutes = false;
        if (scan.HasGap(index, ":") && scan.Number(next) is { } digits)
        {
            if (digits > 59)
            {
                return null;
            }

            minute = digits;
            hasMinutes = true;
            used.Add(next);
            next++;
        }
        else if (scan.IsWord(next, scan.Vocabulary.And) && scan.IsWord(next + 1, scan.Vocabulary.Half))
        {
            minute = 30;
            hasMinutes = true;
            used.AddRange([next, next + 1]);
            next += 2;
        }
        else if (scan.IsWord(next, scan.Vocabulary.And) && scan.IsWord(next + 1, scan.Vocabulary.Quarter))
        {
            minute = 15;
            hasMinutes = true;
            used.AddRange([next, next + 1]);
            next += 2;
        }

        var period = DayPeriod.Unknown;
        if (scan.IsWord(next, scan.Vocabulary.PeriodLinks) && scan.IsWord(next + 1, scan.Vocabulary.PeriodMarkers)
            && scan.Vocabulary.Periods.TryGetValue(scan.Word(next + 2), out var found))
        {
            period = found;
            used.AddRange([next, next + 1, next + 2]);
        }

        if (!marked && !hasMinutes && period == DayPeriod.Unknown)
        {
            return null;
        }

        consumed.AddRange(used);
        return new WhenMention(null, new TimeOnly(Resolve(hour, period), minute), period);
    }

    /// <summary>La regla 6: una parte del día sola, como «en la noche».</summary>
    /// <param name="scan">La frase.</param>
    /// <param name="at">La posición de la preposición.</param>
    /// <param name="consumed">Las posiciones usadas.</param>
    /// <returns>El momento, con solo la parte del día, o <see langword="null"/>.</returns>
    internal static WhenMention? TryPeriod(WhenScan scan, int at, List<int> consumed)
    {
        if (!scan.IsWord(at, scan.Vocabulary.PeriodLinks) || !scan.IsWord(at + 1, scan.Vocabulary.PeriodMarkers))
        {
            return null;
        }

        if (!scan.Vocabulary.Periods.TryGetValue(scan.Word(at + 2), out var period))
        {
            return null;
        }

        consumed.AddRange([at, at + 1, at + 2]);
        return new WhenMention(null, null, period);
    }

    // De la parte del día a la hora de 24: Afternoon con 3 es las 15:00; Morning con 12, las 0:00.
    private static int Resolve(int hour, DayPeriod period) => period switch
    {
        DayPeriod.Afternoon when hour < 12 => hour + 12,
        DayPeriod.Morning when hour == 12 => 0,
        _ => hour,
    };

}
