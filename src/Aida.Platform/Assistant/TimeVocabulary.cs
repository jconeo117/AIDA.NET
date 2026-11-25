using Aida.Engine.Text;
using Aida.Platform.Packs;
using YamlDotNet.RepresentationModel;

namespace Aida.Platform.Assistant;

/// <summary>Las palabras del tiempo que entiende el asistente, plegadas.</summary>
/// <remarks>
/// <para>Es la sección <c>time</c> de <c>packs/platform/time.yaml</c>. Vive en la plataforma porque el tiempo es de todos
/// los brazos, y el pack de plataforma es el único sitio donde se declara (ADR 0037, D4): ningún brazo repite las palabras.</para>
/// <para>En N012 solo se exponen las palabras para que no se tomen por un nombre. Los marcadores y la conjunción
/// (<c>period_markers</c>, <c>and</c>, <c>hour_markers</c>) no entran: son palabras vacías. N013 añadirá sobre este mismo
/// archivo lo que hace falta para leer fechas y horas.</para>
/// </remarks>
public sealed class TimeVocabulary
{
    private TimeVocabulary(IReadOnlySet<string> words) => Words = words;

    /// <summary>Las palabras del tiempo, plegadas y partidas: «pasado mañana» da «pasado» y «manana».</summary>
    public IReadOnlySet<string> Words { get; }

    /// <summary>Lee la sección <c>time</c> de un archivo de pack.</summary>
    /// <param name="path">La ruta de <c>time.yaml</c>.</param>
    /// <returns>El vocabulario.</returns>
    /// <exception cref="InvalidDataException">Si falta la sección o alguna de sus listas o mapas tiene otra forma. El
    /// mensaje empieza por la ruta.</exception>
    public static TimeVocabulary Load(string path)
    {
        var root = PlatformYaml.ReadRoot(path);
        if (PlatformYaml.Child(root, "time") is not YamlMappingNode time)
        {
            throw new InvalidDataException($"{path}: falta la sección 'time'");
        }

        var words = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in new[] { "relative_days", "weekdays", "months", "periods" })
        {
            AddKeys(time, section, path, words);
        }

        foreach (var section in new[] { "noon", "half", "quarter" })
        {
            AddItems(time, section, path, words);
        }

        foreach (var section in new[] { "period_markers", "and", "hour_markers" })
        {
            RequireList(time, section, path);
        }

        return new TimeVocabulary(words);
    }

    private static void AddKeys(YamlMappingNode time, string section, string path, HashSet<string> words)
    {
        if (PlatformYaml.Child(time, section) is not YamlMappingNode map || map.Children.Values.Any(value => value is not YamlScalarNode))
        {
            throw new InvalidDataException($"{path}: 'time.{section}' tiene que ser un mapa de valores simples");
        }

        foreach (var key in map.Children.Keys)
        {
            AddWords(PlatformYaml.Text(key), words);
        }
    }

    private static void AddItems(YamlMappingNode time, string section, string path, HashSet<string> words)
    {
        foreach (var item in RequireList(time, section, path).Children)
        {
            AddWords(PlatformYaml.Text(item), words);
        }
    }

    private static YamlSequenceNode RequireList(YamlMappingNode time, string section, string path) =>
        PlatformYaml.Child(time, section) as YamlSequenceNode
        ?? throw new InvalidDataException($"{path}: 'time.{section}' tiene que ser una lista");

    private static void AddWords(string? text, HashSet<string> words)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var token in TextTokens.Tokenize(text))
        {
            words.Add(TextFold.Fold(token));
        }
    }
}
