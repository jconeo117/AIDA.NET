using System.Globalization;
using Aida.Engine.Text;
using Aida.Platform.Packs;
using YamlDotNet.RepresentationModel;

namespace Aida.Platform.Assistant;

/// <summary>Lee las secciones de <c>time.yaml</c> y comprueba su forma y sus valores.</summary>
/// <remarks>Aparte de <see cref="TimeVocabulary"/> para que cada archivo haga una sola cosa: el vocabulario guarda lo leído,
/// y aquí se decide qué cuenta como error. Un valor desconocido revienta al arrancar, con la ruta, en vez de hacer que el
/// asistente no entienda una fecha sin decir por qué.</remarks>
internal static class TimeVocabularyReader
{
    /// <summary>La sección <c>time</c> del archivo.</summary>
    /// <param name="path">La ruta del archivo.</param>
    /// <returns>El mapa de la sección.</returns>
    /// <exception cref="InvalidDataException">Si falta la sección.</exception>
    internal static YamlMappingNode Section(string path) =>
        PlatformYaml.Child(PlatformYaml.ReadRoot(path), "time") as YamlMappingNode
        ?? throw new InvalidDataException($"{path}: falta la sección 'time'");

    /// <summary>Un mapa de palabras a valores, con las palabras plegadas.</summary>
    /// <typeparam name="T">El tipo del valor.</typeparam>
    /// <param name="time">La sección.</param>
    /// <param name="section">El nombre del mapa.</param>
    /// <param name="path">La ruta, para el mensaje.</param>
    /// <param name="parse">Lee un valor, o nada si no es válido.</param>
    /// <returns>El mapa, por palabra plegada.</returns>
    /// <exception cref="InvalidDataException">Si el mapa no tiene esa forma, un valor no es válido o una clave se repite.</exception>
    internal static Dictionary<string, T> Map<T>(YamlMappingNode time, string section, string path, Func<string, T?> parse)
        where T : struct
    {
        if (PlatformYaml.Child(time, section) is not YamlMappingNode map || map.Children.Values.Any(value => value is not YamlScalarNode))
        {
            throw new InvalidDataException($"{path}: 'time.{section}' tiene que ser un mapa de valores simples");
        }

        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var pair in map.Children)
        {
            var key = Fold(PlatformYaml.Text(pair.Key))
                ?? throw new InvalidDataException($"{path}: 'time.{section}' tiene una clave vacía");
            var value = parse(PlatformYaml.Text(pair.Value) ?? string.Empty)
                ?? throw new InvalidDataException($"{path}: 'time.{section}.{key}' tiene un valor que no es válido");
            if (!result.TryAdd(key, value))
            {
                throw new InvalidDataException($"{path}: 'time.{section}' repite la clave «{key}» una vez plegada");
            }
        }

        return result;
    }

    /// <summary>Una lista de palabras, cada una partida y plegada.</summary>
    /// <param name="time">La sección.</param>
    /// <param name="section">El nombre de la lista.</param>
    /// <param name="path">La ruta, para el mensaje.</param>
    /// <returns>Las palabras plegadas.</returns>
    /// <exception cref="InvalidDataException">Si no es una lista de textos con palabras.</exception>
    internal static HashSet<string> Set(YamlMappingNode time, string section, string path)
    {
        if (PlatformYaml.Child(time, section) is not YamlSequenceNode list)
        {
            throw new InvalidDataException($"{path}: 'time.{section}' tiene que ser una lista");
        }

        var words = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in list.Children)
        {
            var folded = Fold(PlatformYaml.Text(item))
                ?? throw new InvalidDataException($"{path}: 'time.{section}' tiene un elemento vacío o que no es un texto");
            words.UnionWith(folded.Split(' '));
        }

        return words;
    }

    /// <summary>El valor de un día relativo: un entero de 0 a 7.</summary>
    /// <param name="text">El texto del valor.</param>
    /// <returns>El valor, o nada si no es un entero de 0 a 7.</returns>
    internal static int? RelativeDay(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) && days is >= 0 and <= 7 ? days : null;

    /// <summary>El valor de un día de la semana: su nombre en inglés, como <c>monday</c>.</summary>
    /// <param name="text">El texto del valor.</param>
    /// <returns>El día, o nada si no es un nombre de día.</returns>
    internal static DayOfWeek? Weekday(string text) =>
        Enum.GetNames<DayOfWeek>().Contains(text, StringComparer.OrdinalIgnoreCase) ? Enum.Parse<DayOfWeek>(text, ignoreCase: true) : null;

    /// <summary>El valor de un mes: de 1 a 12.</summary>
    /// <param name="text">El texto del valor.</param>
    /// <returns>El mes, o nada si no está entre 1 y 12.</returns>
    internal static int? Month(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var month) && month is >= 1 and <= 12 ? month : null;

    /// <summary>El valor de una parte del día: <c>morning</c> o <c>afternoon</c>.</summary>
    /// <param name="text">El texto del valor.</param>
    /// <returns>La parte, o nada si no es una de las dos.</returns>
    internal static DayPeriod? Period(string text) => text.ToLowerInvariant() switch
    {
        "morning" => DayPeriod.Morning,
        "afternoon" => DayPeriod.Afternoon,
        _ => null,
    };

    private static string? Fold(string? text)
    {
        var tokens = TextTokens.Tokenize(text ?? string.Empty).Select(TextFold.Fold).ToList();
        return tokens.Count == 0 ? null : string.Join(' ', tokens);
    }
}
