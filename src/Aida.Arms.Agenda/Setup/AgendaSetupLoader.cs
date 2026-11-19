using Aida.Platform.Packs;
using YamlDotNet.RepresentationModel;

namespace Aida.Arms.Agenda.Setup;

/// <summary>Lee la configuración de la agenda de la clínica, y la crea desde la plantilla del sector la primera vez.</summary>
/// <remarks>Es <c>load_setup</c> de la 0.1 (<c>setup.py:67-144</c>). Revienta al arrancar si el archivo no cuadra: un horario
/// al revés se notaría al agendar, con alguien esperando. Se lee una vez al arrancar; recargar sin reiniciar llega con el
/// editor de Ajustes.</remarks>
public static class AgendaSetupLoader
{
    /// <summary>Lee la configuración; si no existe, copia antes la plantilla del sector.</summary>
    /// <param name="configFile">La ruta de la configuración de la clínica (<c>&lt;datos&gt;/config/agenda.yaml</c>).</param>
    /// <param name="armPackDirectory">La carpeta del pack de la agenda (<c>packs/agenda</c>).</param>
    /// <param name="defaultSector">El sector de la plantilla, de <c>arm.yaml</c>.</param>
    /// <returns>La configuración.</returns>
    /// <exception cref="InvalidDataException">Si el archivo no cuadra; el mensaje empieza por su ruta.</exception>
    public static AgendaSetup Load(string configFile, string armPackDirectory, string defaultSector)
    {
        if (!File.Exists(configFile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(configFile))!);
            File.Copy(Path.Combine(armPackDirectory, defaultSector, "agenda.yaml"), configFile);
        }

        return Parse(PlatformYaml.ReadRoot(configFile), configFile);
    }

    /// <summary>Convierte el mapa raíz en la configuración, con las comprobaciones de la 0.1.</summary>
    /// <param name="root">El mapa raíz.</param>
    /// <param name="path">La ruta, para los mensajes.</param>
    /// <returns>La configuración.</returns>
    /// <exception cref="InvalidDataException">Si no cuadra.</exception>
    public static AgendaSetup Parse(YamlMappingNode root, string path)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!SetupYaml.TryPositive(PlatformYaml.Child(root, "slot_minutes"), out var slot))
        {
            throw new InvalidDataException($"{path}: 'slot_minutes' debe ser un entero positivo");
        }

        var services = new List<ServiceSpec>();
        foreach (var node in PlatformYaml.Child(root, "services") as YamlSequenceNode ?? [])
        {
            if (node is not YamlMappingNode service)
            {
                throw new InvalidDataException($"{path}: cada servicio es un mapa");
            }

            var id = SetupYaml.RequiredText(service, "id", path);
            if (!SetupYaml.TryPositive(PlatformYaml.Child(service, "minutes"), out var minutes))
            {
                throw new InvalidDataException($"{path}: el servicio «{id}» necesita minutos positivos");
            }

            services.Add(new ServiceSpec(id, SetupYaml.RequiredText(service, "label", path), minutes));
        }

        var serviceIds = services.Select(service => service.Id).ToHashSet(StringComparer.Ordinal);
        var resources = (PlatformYaml.Child(root, "resources") as YamlSequenceNode ?? [])
            .Select(node => ResourceReader.Read(node, serviceIds, slot, path))
            .ToList();
        return new AgendaSetup(SetupYaml.RequiredText(root, "sector", path), slot, SetupYaml.RequiredText(root, "client_label", path), services, resources);
    }
}
