using System.Globalization;
using Aida.Engine.Text;
using Aida.Platform.Packs;
using YamlDotNet.RepresentationModel;

namespace Aida.Platform.Assistant;

/// <summary>Lo que entiende el asistente de un brazo: su umbral, sus palabras vacías y sus intenciones con ejemplos.</summary>
/// <remarks>
/// <para>Un brazo declara sus palabras vacías en sus datos: la 0.1 calibró la intención con todas las palabras, y el
/// debate de N012 midió que con las vacías del brazo el clasificador acierta 14 de 15 frases, y sin ellas 10 (D10,
/// ADR 0037). Por eso el catálogo es de datos y no del motor.</para>
/// <para>La lista cerrada de intenciones la da el brazo en código, en sus declaraciones (ADR 0035): un pack no puede
/// declarar una intención que el código no sabe atender. Se comprueba al cargar, y un pack mal escrito impide
/// arrancar, como los demás.</para>
/// </remarks>
public sealed class IntentCatalog
{
    private IntentCatalog(double threshold, IReadOnlySet<string> stopwords, IReadOnlyList<IntentExamples> intents)
    {
        Threshold = threshold;
        Stopwords = stopwords;
        Intents = intents;
    }

    /// <summary>La puntuación mínima para entender una intención, entre 0 y 1.</summary>
    public double Threshold { get; }

    /// <summary>Las palabras vacías, ya plegadas.</summary>
    public IReadOnlySet<string> Stopwords { get; }

    /// <summary>Las intenciones, en el orden del YAML.</summary>
    public IReadOnlyList<IntentExamples> Intents { get; }

    /// <summary>Lee el catálogo de un pack de brazo.</summary>
    /// <param name="path">La ruta de <c>intents.yaml</c>.</param>
    /// <param name="declared">Las intenciones que el brazo declara en código.</param>
    /// <returns>El catálogo.</returns>
    /// <exception cref="InvalidDataException">Si falta algo, tiene otra forma o una intención no está declarada. El
    /// mensaje empieza por la ruta.</exception>
    public static IntentCatalog Load(string path, IReadOnlyCollection<string> declared)
    {
        ArgumentNullException.ThrowIfNull(declared);
        var root = PlatformYaml.ReadRoot(path);
        return new IntentCatalog(
            ReadThreshold(root, path),
            ReadStopwords(root, path),
            ReadIntents(root, path, declared));
    }

    private static double ReadThreshold(YamlMappingNode root, string path)
    {
        var text = PlatformYaml.Text(PlatformYaml.Child(root, "threshold"));
        return text is not null && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0 && value <= 1
            ? value
            : throw new InvalidDataException($"{path}: 'threshold' tiene que ser un número en (0, 1]");
    }

    private static IReadOnlySet<string> ReadStopwords(YamlMappingNode root, string path)
    {
        if (PlatformYaml.Child(root, "stopwords") is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (PlatformYaml.Child(root, "stopwords") is not YamlSequenceNode list)
        {
            throw new InvalidDataException($"{path}: 'stopwords' tiene que ser una lista de textos");
        }

        return list.Children
            .Select(node => ReadText(node, path, "'stopwords' tiene un elemento vacío o que no es un texto"))
            .Select(TextFold.Fold)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<IntentExamples> ReadIntents(YamlMappingNode root, string path, IReadOnlyCollection<string> declared)
    {
        if (PlatformYaml.Child(root, "intents") is not YamlSequenceNode list || list.Children.Count == 0)
        {
            throw new InvalidDataException($"{path}: falta la lista 'intents' o está vacía");
        }

        var allowed = declared.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var intents = new List<IntentExamples>();
        foreach (var node in list.Children)
        {
            if (node is not YamlMappingNode map)
            {
                throw new InvalidDataException($"{path}: cada intención es un mapa");
            }

            var id = PlatformYaml.Text(PlatformYaml.Child(map, "id"));
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidDataException($"{path}: una intención no tiene 'id'");
            }

            if (!allowed.Contains(id))
            {
                throw new InvalidDataException($"{path}: la intención '{id}' no está declarada por el brazo");
            }

            if (!seen.Add(id))
            {
                throw new InvalidDataException($"{path}: la intención '{id}' está repetida");
            }

            intents.Add(new IntentExamples(id, ReadExamples(map, id, path)));
        }

        return intents;
    }

    private static IReadOnlyList<string> ReadExamples(YamlMappingNode map, string id, string path)
    {
        if (PlatformYaml.Child(map, "examples") is not YamlSequenceNode list || list.Children.Count == 0)
        {
            throw new InvalidDataException($"{path}: la intención '{id}' no tiene ejemplos");
        }

        return list.Children
            .Select(node => ReadText(node, path, $"la intención '{id}' tiene un ejemplo vacío"))
            .ToList();
    }

    private static string ReadText(YamlNode node, string path, string problem)
    {
        var text = PlatformYaml.Text(node);
        return string.IsNullOrWhiteSpace(text) ? throw new InvalidDataException($"{path}: {problem}") : text;
    }
}
