using System.Text.RegularExpressions;
using Aida.Platform.Packs;
using YamlDotNet.RepresentationModel;

namespace Aida.Platform.People;

/// <summary>Los tipos de documento y los sexos, de <c>packs/platform/people.yaml</c>.</summary>
/// <remarks>Van en un pack porque cambian con el país y con la norma, no con el código (ADR 0038, lección 15). El orden es el
/// del pack, para que la lista que pinta la ficha sea siempre la misma. Quien guarda un dato valida que su id esté aquí; una
/// norma nueva se cambia en el pack y no en el código.</remarks>
public sealed partial class PersonCatalog
{
    private PersonCatalog(IReadOnlyList<(string Id, string Label)> documentTypes, IReadOnlyList<(string Id, string Label)> sexes)
    {
        DocumentTypes = documentTypes;
        Sexes = sexes;
    }

    /// <summary>Los tipos de documento, en el orden del pack.</summary>
    public IReadOnlyList<(string Id, string Label)> DocumentTypes { get; }

    /// <summary>Los sexos, en el orden del pack.</summary>
    public IReadOnlyList<(string Id, string Label)> Sexes { get; }

    /// <summary>Lee el catálogo del pack.</summary>
    /// <param name="path">La ruta de <c>people.yaml</c>.</param>
    /// <returns>El catálogo.</returns>
    /// <exception cref="InvalidDataException">Si falta una lista, si un id está vacío, repetido o no casa con
    /// <c>^[A-Z]{1,4}$</c>, o si una etiqueta falta. El texto del error empieza por la ruta: un hueco en la lista se descubriría
    /// con alguien delante.</exception>
    public static PersonCatalog Load(string path)
    {
        var root = PlatformYaml.ReadRoot(path);
        if (PlatformYaml.Child(root, "people") is not YamlMappingNode section)
        {
            throw new InvalidDataException($"{path}: falta la sección 'people'");
        }

        return new PersonCatalog(ReadList(path, section, "document_types"), ReadList(path, section, "sexes"));
    }

    /// <summary>Si ese tipo de documento está en el pack.</summary>
    /// <param name="id">El id.</param>
    /// <returns><see langword="true"/> si está.</returns>
    public bool HasDocumentType(string id) => DocumentTypes.Any(entry => entry.Id == id);

    /// <summary>Si ese sexo está en el pack.</summary>
    /// <param name="id">El id.</param>
    /// <returns><see langword="true"/> si está.</returns>
    public bool HasSex(string id) => Sexes.Any(entry => entry.Id == id);

    private static IReadOnlyList<(string Id, string Label)> ReadList(string path, YamlMappingNode section, string key)
    {
        if (PlatformYaml.Child(section, key) is not YamlSequenceNode items)
        {
            throw new InvalidDataException($"{path}: falta la lista '{key}'");
        }

        var entries = new List<(string Id, string Label)>();
        foreach (var item in items.Children)
        {
            if (item is not YamlMappingNode map)
            {
                throw new InvalidDataException($"{path}: cada entrada de '{key}' es un mapa con id y label");
            }

            var id = PlatformYaml.Text(PlatformYaml.Child(map, "id")) ?? string.Empty;
            var label = PlatformYaml.Text(PlatformYaml.Child(map, "label")) ?? string.Empty;
            if (!IdPattern().IsMatch(id))
            {
                throw new InvalidDataException($"{path}: en '{key}', el id «{id}» no es válido (^[A-Z]{{1,4}}$)");
            }

            if (entries.Any(entry => entry.Id == id))
            {
                throw new InvalidDataException($"{path}: en '{key}', el id «{id}» está repetido");
            }

            if (label.Length == 0)
            {
                throw new InvalidDataException($"{path}: en '{key}', el id «{id}» no tiene etiqueta");
            }

            entries.Add((id, label));
        }

        return entries;
    }

    [GeneratedRegex("^[A-Z]{1,4}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
