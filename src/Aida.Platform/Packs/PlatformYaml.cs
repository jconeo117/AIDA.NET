using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Aida.Platform.Packs;

/// <summary>Lee un archivo YAML de un pack a su mapa raíz: los de <c>packs/platform/</c> y los de la carpeta de cada brazo.</summary>
/// <remarks>Solo el modelo de nodos de YamlDotNet, como en el motor (ADR 0024, ampliado por el 0031 y el 0033), nunca su
/// deserialización a objetos. Es la única puerta de YAML fuera del motor: los brazos la usan para su pack y no
/// referencian YamlDotNet. Estos archivos solo tienen mapas, listas, textos y números: lo demás falla con su ruta.</remarks>
public static class PlatformYaml
{
    /// <summary>Lee un archivo a su mapa raíz.</summary>
    /// <param name="path">La ruta del archivo.</param>
    /// <returns>El mapa raíz.</returns>
    /// <exception cref="InvalidDataException">Si no se puede leer, no es YAML válido o no es un único mapa; el mensaje empieza
    /// por la ruta.</exception>
    public static YamlMappingNode ReadRoot(string path)
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

    /// <summary>El hijo de un mapa con esa clave.</summary>
    /// <param name="map">El mapa.</param>
    /// <param name="key">La clave.</param>
    /// <returns>El nodo, o <see langword="null"/> si no está.</returns>
    public static YamlNode? Child(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var node) ? node : null;

    /// <summary>El texto de un escalar.</summary>
    /// <param name="node">El nodo.</param>
    /// <returns>El texto de un escalar entre comillas (aunque esté vacío) o de un escalar sin comillas no vacío;
    /// <see langword="null"/> si no es un escalar o es un escalar sin comillas vacío.</returns>
    public static string? Text(YamlNode? node) =>
        node is YamlScalarNode { Style: not ScalarStyle.Plain } quoted ? quoted.Value
        : node is YamlScalarNode plain && plain.Value is { Length: > 0 } value ? value : null;
}
