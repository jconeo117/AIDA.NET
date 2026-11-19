using Aida.Engine.Actions;
using Aida.Platform.Packs;
using YamlDotNet.RepresentationModel;

namespace Aida.Arms.Agenda.Setup;

/// <summary>Lo que dice <c>packs/agenda/arm.yaml</c> y lee la agenda: el sector de la plantilla y las acciones.</summary>
/// <remarks>Las acciones y su reversibilidad las declara el pack (ADR 0035); la puerta del motor solo ejecuta lo que está
/// aquí. El resto del manifiesto (nombre, icono, punto de entrada) es del registro de brazos, que llega después.</remarks>
/// <param name="DefaultSector">El sector cuya plantilla se copia la primera vez.</param>
/// <param name="Actions">Las acciones del brazo con su reversibilidad.</param>
public sealed record AgendaManifest(string DefaultSector, ActionCatalog Actions)
{
    /// <summary>Lee el manifiesto.</summary>
    /// <param name="path">La ruta de <c>arm.yaml</c>.</param>
    /// <returns>El manifiesto.</returns>
    /// <exception cref="InvalidDataException">Si falta el sector o las acciones no cuadran.</exception>
    public static AgendaManifest Load(string path)
    {
        var root = PlatformYaml.ReadRoot(path);
        var sector = SetupYaml.RequiredText(root, "default_sector", path);
        if (PlatformYaml.Child(root, "actions") is not YamlSequenceNode list)
        {
            throw new InvalidDataException($"{path}: falta la lista 'actions'");
        }

        var declared = list.Select(node => node is YamlMappingNode map
            ? KeyValuePair.Create(PlatformYaml.Text(PlatformYaml.Child(map, "id")) ?? string.Empty, PlatformYaml.Text(PlatformYaml.Child(map, "reversibility")) ?? string.Empty)
            : throw new InvalidDataException($"{path}: cada acción es un mapa"));
        return new AgendaManifest(sector, ActionCatalog.Parse(declared, path));
    }
}
