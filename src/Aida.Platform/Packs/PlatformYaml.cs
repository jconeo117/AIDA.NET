using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Aida.Platform.Packs;

/// <summary>Lee un archivo de <c>packs/platform/</c> a su mapa raíz.</summary>
/// <remarks>Solo el modelo de nodos de YamlDotNet, como en el motor (ADR 0024, ampliado por el ADR 0031). Los archivos
/// de la plataforma solo tienen mapas, listas, textos y números: lo demás falla con su ruta.</remarks>
internal static class PlatformYaml
{
    internal static YamlMappingNode ReadRoot(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"{path}: no se pudo leer: {exception.Message}");
        }

        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text));
        }
        catch (YamlException exception)
        {
            throw new InvalidDataException($"{path}: no es YAML válido: {exception.Message}");
        }

        if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new InvalidDataException($"{path}: tiene que ser un único mapa");
        }

        return root;
    }

    internal static YamlNode? Child(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var node) ? node : null;

    internal static string? Text(YamlNode? node) =>
        node is YamlScalarNode { Style: not ScalarStyle.Plain } quoted ? quoted.Value
        : node is YamlScalarNode plain && plain.Value is { Length: > 0 } value ? value : null;
}
