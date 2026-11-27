using Aida.Engine.Actions;
using Aida.Platform.Packs;
using YamlDotNet.RepresentationModel;

namespace Aida.Arms.Health.Setup;

/// <summary>Lo que dice <c>packs/health/arm.yaml</c> y lee la historia clínica: las acciones y su reversibilidad.</summary>
/// <remarks>Las acciones y su reversibilidad las declara el pack (ADR 0035); la puerta del motor solo ejecuta lo que está
/// aquí. Firmar aparece como irreversible: por encima del techo del brazo, solo pasa con la autorización de quien firma (ADR 0039).</remarks>
/// <param name="Actions">Las acciones del brazo con su reversibilidad.</param>
public sealed record HealthManifest(ActionCatalog Actions)
{
    /// <summary>Lee el manifiesto.</summary>
    /// <param name="path">La ruta de <c>arm.yaml</c>.</param>
    /// <returns>El manifiesto.</returns>
    /// <exception cref="InvalidDataException">Si falta la lista de acciones o alguna no es un mapa.</exception>
    public static HealthManifest Load(string path)
    {
        var root = PlatformYaml.ReadRoot(path);
        if (PlatformYaml.Child(root, "actions") is not YamlSequenceNode list)
        {
            throw new InvalidDataException($"{path}: falta la lista 'actions'");
        }

        var declared = list.Select(node => node is YamlMappingNode map
            ? KeyValuePair.Create(PlatformYaml.Text(PlatformYaml.Child(map, "id")) ?? string.Empty, PlatformYaml.Text(PlatformYaml.Child(map, "reversibility")) ?? string.Empty)
            : throw new InvalidDataException($"{path}: cada acción es un mapa"));
        return new HealthManifest(ActionCatalog.Parse(declared, path));
    }
}
