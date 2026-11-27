using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Aida.Platform.Packs;
using YamlDotNet.RepresentationModel;

namespace Aida.Arms.Health.Setup;

/// <summary>Los apartados, el texto de «No consta.» y los textos de pantalla de la atención, de <c>packs/health/encounter.yaml</c>.</summary>
/// <remarks>
/// <para>Los textos van en el pack y no en el código (lección 15): las palabras del idioma no se escriben en un literal. Por
/// eso un archivo sin uno de los textos de <see cref="RequiredKeys"/> no arranca: la pantalla no puede quedarse con una
/// etiqueta en blanco delante de la persona que firma.</para>
/// <para>Los apartados se leen en el orden del archivo y no se ordenan: ese orden es el que ve quien escribe y el que se
/// muestra al firmar.</para>
/// </remarks>
public sealed class EncounterTemplate
{
    private static readonly Regex _sectionId = new("^[a-z][a-z_]*$", RegexOptions.CultureInvariant);

    private readonly FrozenDictionary<string, string> _texts;

    private EncounterTemplate(string notRecorded, IReadOnlyList<EncounterSection> sections, FrozenDictionary<string, string> texts)
    {
        NotRecorded = notRecorded;
        Sections = sections;
        _texts = texts;
    }

    /// <summary>Las claves de texto que el archivo debe traer; sin una, no arranca.</summary>
    /// <remarks>Incluye los motivos de rechazo que la pantalla explica con su propio texto. Los demás motivos caen en
    /// <c>reason_failed</c>, que sí es obligatorio.</remarks>
    public static IReadOnlyList<string> RequiredKeys { get; } =
    [
        "title", "card_link", "new_button", "save_button", "review_button", "sign_button", "password_label", "missing_warning",
        "draft_of", "signed_by", "hash_label", "back_link", "empty_list", "complete_link",
        "reason_incomplete_person", "reason_bad_password", "reason_wait", "reason_forbidden", "reason_failed",
    ];

    /// <summary>El texto que se firma en un apartado obligatorio vacío.</summary>
    public string NotRecorded { get; }

    /// <summary>Los apartados, en el orden del archivo.</summary>
    public IReadOnlyList<EncounterSection> Sections { get; }

    /// <summary>Un texto de pantalla por su clave.</summary>
    /// <param name="key">La clave, de <see cref="RequiredKeys"/> o de un apartado del pack.</param>
    /// <returns>El texto.</returns>
    /// <exception cref="KeyNotFoundException">Si la clave no está en el archivo.</exception>
    public string this[string key] => _texts[key];

    /// <summary>El texto que explica un motivo de rechazo (<c>reason_&lt;código&gt;</c>).</summary>
    /// <param name="reason">El código del motivo.</param>
    /// <returns>El texto del motivo, o el de <c>reason_failed</c> si el motivo no tiene el suyo.</returns>
    public string Reason(string reason) => _texts.GetValueOrDefault($"reason_{reason}") ?? _texts["reason_failed"];

    /// <summary>Lee la plantilla.</summary>
    /// <param name="path">La ruta de <c>encounter.yaml</c>.</param>
    /// <returns>La plantilla.</returns>
    /// <exception cref="InvalidDataException">Si falta algo, si un apartado se repite o su id no cuadra, o si falta un texto
    /// de <see cref="RequiredKeys"/>. El mensaje dice la ruta.</exception>
    public static EncounterTemplate Load(string path)
    {
        var root = PlatformYaml.ReadRoot(path);
        var notRecorded = PlatformYaml.Text(PlatformYaml.Child(root, "not_recorded"))
            ?? throw new InvalidDataException($"{path}: falta 'not_recorded'");
        return new EncounterTemplate(notRecorded, ReadSections(root, path), ReadTexts(root, path));
    }

    private static IReadOnlyList<EncounterSection> ReadSections(YamlMappingNode root, string path)
    {
        if (PlatformYaml.Child(root, "sections") is not YamlSequenceNode list)
        {
            throw new InvalidDataException($"{path}: falta la lista 'sections'");
        }

        var found = new List<EncounterSection>();
        foreach (var node in list)
        {
            if (node is not YamlMappingNode map)
            {
                throw new InvalidDataException($"{path}: cada apartado es un mapa");
            }

            var id = Required(map, "id", path);
            if (!_sectionId.IsMatch(id))
            {
                throw new InvalidDataException($"{path}: el apartado «{id}» no es un identificador válido (^[a-z][a-z_]*$)");
            }

            if (found.Any(section => section.Id == id))
            {
                throw new InvalidDataException($"{path}: el apartado «{id}» aparece dos veces");
            }

            var flag = Required(map, "required", path);
            if (flag is not ("true" or "false"))
            {
                throw new InvalidDataException($"{path}: 'required' de «{id}» debe ser true o false");
            }

            found.Add(new EncounterSection(id, Required(map, "label", path), flag == "true"));
        }

        return found;
    }

    private static FrozenDictionary<string, string> ReadTexts(YamlMappingNode root, string path)
    {
        if (PlatformYaml.Child(root, "texts") is not YamlMappingNode map)
        {
            throw new InvalidDataException($"{path}: falta la sección 'texts'");
        }

        var texts = map.Children
            .Select(pair => KeyValuePair.Create(PlatformYaml.Text(pair.Key) ?? string.Empty, PlatformYaml.Text(pair.Value)))
            .Where(pair => pair.Value is not null)
            .ToFrozenDictionary(pair => pair.Key, pair => pair.Value!, StringComparer.Ordinal);
        var missing = RequiredKeys.Where(key => !texts.ContainsKey(key)).ToList();
        return missing.Count == 0
            ? texts
            : throw new InvalidDataException($"{path}: faltan textos de la atención: {string.Join(", ", missing)}");
    }

    private static string Required(YamlMappingNode map, string key, string path) =>
        PlatformYaml.Text(PlatformYaml.Child(map, key)) ?? throw new InvalidDataException($"{path}: falta '{key}' en un apartado");
}
