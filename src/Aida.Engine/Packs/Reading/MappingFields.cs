using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Los campos de una definición, leídos en el orden de su clase y seguidos de las claves que sobran.</summary>
/// <remarks>
/// Así lo hace pydantic: primero los campos en el orden en que la clase los declara, y luego las claves desconocidas
/// en el orden del YAML (<c>extra_forbidden</c>). Un campo obligatorio que falta da un problema en su posición.
/// </remarks>
internal sealed class MappingFields
{
    private readonly YamlMappingNode _mapping;
    private readonly string _path;
    private readonly ProblemList _problems;
    private readonly List<string> _declared = [];

    /// <summary>Prepara la lectura de un mapa.</summary>
    /// <param name="mapping">El mapa de la definición.</param>
    /// <param name="path">La ruta de la definición.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    internal MappingFields(YamlMappingNode mapping, string path, ProblemList problems)
    {
        _mapping = mapping;
        _path = path;
        _problems = problems;
    }

    /// <summary>Lee un campo obligatorio. Si falta, anota el problema y devuelve el valor de relleno.</summary>
    /// <typeparam name="T">El tipo del campo.</typeparam>
    /// <param name="name">El nombre del campo en el YAML.</param>
    /// <param name="read">El conversor.</param>
    /// <returns>El valor convertido, o el de relleno si no hay valor válido.</returns>
    internal T Required<T>(string name, NodeReader<T> read)
    {
        _declared.Add(name);
        if (TryFind(name, out var node))
        {
            read(node, ProblemList.Join(_path, name), _problems, out var value);
            return value;
        }

        _problems.Add(ProblemList.Join(_path, name), "Field required");
        return default!;
    }

    /// <summary>Lee un campo con valor por defecto. Si falta, devuelve el por defecto sin problema.</summary>
    /// <typeparam name="T">El tipo del campo.</typeparam>
    /// <param name="name">El nombre del campo en el YAML.</param>
    /// <param name="read">El conversor.</param>
    /// <param name="fallback">El valor por defecto.</param>
    /// <returns>El valor convertido, o el por defecto si el campo no está.</returns>
    internal T Optional<T>(string name, NodeReader<T> read, T fallback)
    {
        _declared.Add(name);
        if (!TryFind(name, out var node))
        {
            return fallback;
        }

        read(node, ProblemList.Join(_path, name), _problems, out var value);
        return value;
    }

    /// <summary>Anota las claves que sobran, en el orden del YAML. Debe llamarse después de leer todos los campos.</summary>
    internal void RejectExtras()
    {
        foreach (var pair in _mapping.Children)
        {
            var key = pair.Key is YamlScalarNode scalar ? YamlScalar.TextOf(scalar) : string.Empty;
            if (!_declared.Contains(key))
            {
                _problems.Add(ProblemList.Join(_path, key), "Extra inputs are not permitted");
            }
        }
    }

    private bool TryFind(string name, out YamlNode node)
    {
        foreach (var pair in _mapping.Children)
        {
            if (pair.Key is YamlScalarNode scalar && YamlScalar.TextOf(scalar) == name)
            {
                node = pair.Value;
                return true;
            }
        }

        node = null!;
        return false;
    }
}
