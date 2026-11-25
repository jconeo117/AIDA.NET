using Aida.Platform.Assistant;

namespace Aida.Cognition.Interpretation;

/// <summary>Las reglas de día: una fecha numérica, un día relativo, un día de la semana y un día del mes.</summary>
/// <remarks>
/// <para>Cada regla devuelve el momento que encontró y añade a <c>consumed</c> las posiciones que usó, solo si encaja. Así
/// una regla que no encaja no deja nada a medias.</para>
/// <para>Las fechas sin año siguen el aviso de D19: una fecha que pasó hace poco se avisa, y una que pasó hace más es la del
/// año siguiente. Un día de la semana es siempre el siguiente, nunca hoy (D13).</para>
/// </remarks>
internal static class DayExpressions
{
    /// <summary>Hasta cuántos días pasó una fecha sin año para avisar de que ya pasó, en vez de pasar al año siguiente (D19).</summary>
    internal const int RecentPastDays = 31;

    /// <summary>La regla 1: <c>D/M</c> o <c>D/M/AAAA</c>, con <c>/</c> o <c>-</c>, y nada más entre los números.</summary>
    /// <param name="scan">La frase.</param>
    /// <param name="at">La posición del día.</param>
    /// <param name="consumed">Las posiciones usadas, a las que se añaden las de esta regla si encaja.</param>
    /// <returns>El momento, o <see langword="null"/> si no encaja (también si la fecha es imposible).</returns>
    internal static WhenMention? TryNumericDate(WhenScan scan, int at, List<int> consumed)
    {
        var separator = scan.HasGap(at, "/") ? "/" : scan.HasGap(at, "-") ? "-" : null;
        if (separator is null || scan.Number(at) is not { } day || scan.Number(at + 1) is not { } month)
        {
            return null;
        }

        if (scan.Count > at + 2 && scan.Gap(at + 1) == separator && WhenScan.IsDigits(scan.Spans[at + 2].Token))
        {
            if (scan.Spans[at + 2].Token.Length != 4 || scan.Number(at + 2) is not { } year)
            {
                return null;
            }

            var exact = MakeDate(year, month, day);
            if (exact is null)
            {
                return null;
            }

            consumed.AddRange([at, at + 1, at + 2]);
            return new WhenMention(exact, null, DayPeriod.Unknown);
        }

        var withoutYear = WithoutYear(scan.Today, month, day);
        if (withoutYear is null)
        {
            return null;
        }

        consumed.AddRange([at, at + 1]);
        return new WhenMention(withoutYear, null, DayPeriod.Unknown);
    }

    /// <summary>La regla 2: un día relativo, como «mañana» o «pasado mañana».</summary>
    /// <param name="scan">La frase.</param>
    /// <param name="at">La posición de la primera palabra.</param>
    /// <param name="consumed">Las posiciones usadas.</param>
    /// <returns>El momento, o <see langword="null"/>.</returns>
    /// <remarks>Se prueba la clave más larga primero, para que «pasado mañana» gane a «mañana». «la mañana» no es un día: es una
    /// parte del día, y la regla 6 la lee.</remarks>
    internal static WhenMention? TryRelative(WhenScan scan, int at, List<int> consumed)
    {
        foreach (var (key, days) in scan.Vocabulary.RelativeDays
            .OrderByDescending(pair => pair.Key.Split(' ').Length)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var words = key.Split(' ');
            if (!Matches(scan, at, words))
            {
                continue;
            }

            if (words.Length == 1 && scan.Vocabulary.Periods.ContainsKey(key) && scan.Vocabulary.PeriodMarkers.Contains(scan.Word(at - 1)))
            {
                return null;
            }

            consumed.AddRange(Enumerable.Range(at, words.Length));
            return new WhenMention(scan.Today.AddDays(days), null, DayPeriod.Unknown);
        }

        return null;
    }

    /// <summary>La regla 3: un día de la semana, el siguiente a hoy, con un «el» delante si lo hay.</summary>
    /// <param name="scan">La frase.</param>
    /// <param name="at">La posición del día de la semana.</param>
    /// <param name="consumed">Las posiciones usadas; el «el» de delante se consume solo si no se usó antes.</param>
    /// <returns>El momento, o <see langword="null"/>.</returns>
    internal static WhenMention? TryWeekday(WhenScan scan, int at, List<int> consumed)
    {
        if (!scan.Vocabulary.Weekdays.TryGetValue(scan.Word(at), out var weekday))
        {
            return null;
        }

        var day = scan.Today.AddDays(1);
        while (day.DayOfWeek != weekday)
        {
            day = day.AddDays(1);
        }

        if (scan.Word(at - 1) == "el" && !consumed.Contains(at - 1))
        {
            consumed.Add(at - 1);
        }

        consumed.Add(at);
        return new WhenMention(day, null, DayPeriod.Unknown);
    }

    /// <summary>La regla 4, primera forma: <c>[el] N de &lt;mes&gt;</c>.</summary>
    /// <param name="scan">La frase.</param>
    /// <param name="at">La posición del «el» o del número.</param>
    /// <param name="consumed">Las posiciones usadas.</param>
    /// <returns>El momento, o <see langword="null"/>.</returns>
    internal static WhenMention? TryNamedMonth(WhenScan scan, int at, List<int> consumed)
    {
        var offset = scan.Word(at) == "el" ? 1 : 0;
        var number = at + offset;
        if (scan.Number(number) is not { } day || scan.Word(number + 1) != "de")
        {
            return null;
        }

        if (!scan.Vocabulary.Months.TryGetValue(scan.Word(number + 2), out var month))
        {
            return null;
        }

        var date = WithoutYear(scan.Today, month, day);
        if (date is null)
        {
            return null;
        }

        consumed.AddRange(Enumerable.Range(at, offset + 3));
        return new WhenMention(date, null, DayPeriod.Unknown);
    }

    /// <summary>La regla 4, segunda forma: <c>el N</c>, sin mes ni barra detrás.</summary>
    /// <param name="scan">La frase.</param>
    /// <param name="at">La posición del «el».</param>
    /// <param name="consumed">Las posiciones usadas.</param>
    /// <returns>El momento, o <see langword="null"/>. Es el siguiente día N igual o posterior a hoy.</returns>
    /// <remarks>Con una barra o un guion justo detrás del número, «el 20/11» es una fecha de la regla 1, no un día del mes.</remarks>
    internal static WhenMention? TryDayOfMonth(WhenScan scan, int at, List<int> consumed)
    {
        if (scan.Word(at) != "el" || scan.Number(at + 1) is not { } day || day is < 1 or > 31)
        {
            return null;
        }

        if (scan.HasGap(at + 1, "/") || scan.HasGap(at + 1, "-"))
        {
            return null;
        }

        var date = NextDayOfMonth(scan.Today, day);
        if (date is null)
        {
            return null;
        }

        consumed.AddRange([at, at + 1]);
        return new WhenMention(date, null, DayPeriod.Unknown);
    }

    /// <summary>La fecha de un día y un mes sin año (D19).</summary>
    /// <param name="today">Hoy.</param>
    /// <param name="month">El mes.</param>
    /// <param name="day">El día.</param>
    /// <returns>La fecha de este año; si ya pasó hace más de <see cref="RecentPastDays"/> días, la del año siguiente. Si pasó
    /// hace poco, la misma, anterior a hoy, para que el brazo avise. Nada si la fecha no existe.</returns>
    internal static DateOnly? WithoutYear(DateOnly today, int month, int day)
    {
        var date = MakeDate(today.Year, month, day);
        if (date is null)
        {
            return null;
        }

        if (date.Value >= today)
        {
            return date;
        }

        return today.DayNumber - date.Value.DayNumber <= RecentPastDays ? date : MakeDate(today.Year + 1, month, day);
    }

    private static DateOnly? MakeDate(int year, int month, int day) =>
        year is >= 1 and <= 9999 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateOnly(year, month, day)
            : null;

    private static DateOnly? NextDayOfMonth(DateOnly today, int day)
    {
        var first = new DateOnly(today.Year, today.Month, 1);
        for (var months = 0; months < 13; months++)
        {
            var month = first.AddMonths(months);
            if (MakeDate(month.Year, month.Month, day) is { } date && date >= today)
            {
                return date;
            }
        }

        return null;
    }

    private static bool Matches(WhenScan scan, int at, IReadOnlyList<string> words)
    {
        for (var offset = 0; offset < words.Count; offset++)
        {
            if (scan.Word(at + offset) != words[offset])
            {
                return false;
            }
        }

        return true;
    }
}
