namespace Aida.Platform.Assistant;

/// <summary>Las palabras del tiempo que entiende el asistente, plegadas y ya leídas.</summary>
/// <remarks>
/// <para>Es la sección <c>time</c> de <c>packs/platform/time.yaml</c>. Vive en la plataforma porque el tiempo es de todos
/// los brazos, y el pack de plataforma es el único sitio donde se declara (ADR 0037, D4): ningún brazo repite las palabras.</para>
/// <para>Cada propiedad la lee una pieza concreta: <see cref="Words"/> es lo que no se toma por un nombre; el lector de
/// fechas y horas (<c>WhenReader</c>, en Aida.Cognition) lee el resto. Los errores de la forma salen al cargar, con la
/// ruta, como en los demás packs.</para>
/// </remarks>
public sealed class TimeVocabulary
{
    /// <summary>Las palabras del tiempo, plegadas y partidas: «pasado mañana» da «pasado» y «manana».</summary>
    /// <remarks>Lo lee el papel <c>Time</c> de cada palabra que no consume el lector de fechas, para que no se tome por un nombre.</remarks>
    public IReadOnlySet<string> Words { get; private init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Los días relativos, por su palabra plegada: «hoy» da 0, «pasado manana» da 2.</summary>
    /// <remarks>Lo lee el lector de fechas, que suma el valor a hoy. La clave puede tener dos palabras, separadas por un espacio.</remarks>
    public IReadOnlyDictionary<string, int> RelativeDays { get; private init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Los días de la semana, por su palabra plegada.</summary>
    /// <remarks>Lo lee el lector de fechas, que da el siguiente día con ese nombre, nunca hoy (D13).</remarks>
    public IReadOnlyDictionary<string, DayOfWeek> Weekdays { get; private init; } = new Dictionary<string, DayOfWeek>(StringComparer.Ordinal);

    /// <summary>Los meses, por su palabra plegada, de 1 a 12.</summary>
    /// <remarks>Lo lee el lector de fechas para «el 5 de diciembre».</remarks>
    public IReadOnlyDictionary<string, int> Months { get; private init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Las partes del día, por su palabra plegada.</summary>
    /// <remarks>Lo lee el lector de fechas, que suma 12 a la hora de la tarde (D12) y pone la parte en el momento.</remarks>
    public IReadOnlyDictionary<string, DayPeriod> Periods { get; private init; } = new Dictionary<string, DayPeriod>(StringComparer.Ordinal);

    /// <summary>Las palabras que marcan una parte del día: «la» en «la mañana».</summary>
    /// <remarks>Lo lee el lector de fechas: una parte del día precedida de una de estas no es un día («la mañana»).</remarks>
    public IReadOnlySet<string> PeriodMarkers { get; private init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Las palabras que marcan una hora: «las» y «la» en «a las 10».</summary>
    /// <remarks>Lo lee el lector de fechas, que da por hora un número precedido de una de estas.</remarks>
    public IReadOnlySet<string> HourMarkers { get; private init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>La conjunción de «y media» y «y cuarto».</summary>
    /// <remarks>Lo lee el lector de fechas junto con <see cref="Half"/> y <see cref="Quarter"/>.</remarks>
    public IReadOnlySet<string> And { get; private init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Las palabras que dan la mitad: «media» en «y media».</summary>
    /// <remarks>Lo lee el lector de fechas: «a las 10 y media» es las 10:30.</remarks>
    public IReadOnlySet<string> Half { get; private init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Las palabras que dan el cuarto: «cuarto» en «y cuarto».</summary>
    /// <remarks>Lo lee el lector de fechas: «a las 8 y cuarto» es las 8:15.</remarks>
    public IReadOnlySet<string> Quarter { get; private init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Las palabras que dan las doce en punto: «mediodía».</summary>
    /// <remarks>Lo lee el lector de fechas: una de estas palabras sola es las 12:00.</remarks>
    public IReadOnlySet<string> Noon { get; private init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Lee la sección <c>time</c> de un archivo de pack.</summary>
    /// <param name="path">La ruta de <c>time.yaml</c>.</param>
    /// <returns>El vocabulario.</returns>
    /// <exception cref="InvalidDataException">Si falta la sección, alguna de sus listas o mapas tiene otra forma, un valor
    /// no es válido (un día de la semana, una parte del día, un mes fuera de 1–12, un día relativo fuera de 0–7) o una
    /// clave se repite una vez plegada. El mensaje empieza por la ruta.</exception>
    public static TimeVocabulary Load(string path)
    {
        var time = TimeVocabularyReader.Section(path);
        var relative = TimeVocabularyReader.Map(time, "relative_days", path, TimeVocabularyReader.RelativeDay);
        var weekdays = TimeVocabularyReader.Map(time, "weekdays", path, TimeVocabularyReader.Weekday);
        var months = TimeVocabularyReader.Map(time, "months", path, TimeVocabularyReader.Month);
        var periods = TimeVocabularyReader.Map(time, "periods", path, TimeVocabularyReader.Period);
        var noon = TimeVocabularyReader.Set(time, "noon", path);
        var half = TimeVocabularyReader.Set(time, "half", path);
        var quarter = TimeVocabularyReader.Set(time, "quarter", path);
        var and = TimeVocabularyReader.Set(time, "and", path);
        var hourMarkers = TimeVocabularyReader.Set(time, "hour_markers", path);
        var periodMarkers = TimeVocabularyReader.Set(time, "period_markers", path);

        var words = new HashSet<string>(StringComparer.Ordinal);
        words.UnionWith(relative.Keys.SelectMany(key => key.Split(' ')));
        words.UnionWith(weekdays.Keys);
        words.UnionWith(months.Keys);
        words.UnionWith(periods.Keys);
        words.UnionWith(noon);
        words.UnionWith(half);
        words.UnionWith(quarter);

        return new TimeVocabulary
        {
            Words = words,
            RelativeDays = relative,
            Weekdays = weekdays,
            Months = months,
            Periods = periods,
            PeriodMarkers = periodMarkers,
            HourMarkers = hourMarkers,
            And = and,
            Half = half,
            Quarter = quarter,
            Noon = noon,
        };
    }
}
