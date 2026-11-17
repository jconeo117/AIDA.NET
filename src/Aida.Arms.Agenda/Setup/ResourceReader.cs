using System.Collections.Frozen;
using Aida.Platform.Packs;
using YamlDotNet.RepresentationModel;

namespace Aida.Arms.Agenda.Setup;

/// <summary>Lee un recurso del archivo de la agenda, con sus servicios y su horario.</summary>
/// <remarks>Las mismas comprobaciones de la 0.1 y en el mismo orden (<c>setup.py:81-114</c>): servicios que no existen, días
/// desconocidos, tramos al revés y tramos que no son múltiplo del hueco.</remarks>
internal static class ResourceReader
{
    /// <summary>Los días como los escribe el archivo, en el orden de <c>WEEKDAYS</c> (<c>setup.py:21</c>).</summary>
    private static readonly FrozenDictionary<string, DayOfWeek> _days = new Dictionary<string, DayOfWeek>(StringComparer.Ordinal)
    {
        ["mon"] = DayOfWeek.Monday,
        ["tue"] = DayOfWeek.Tuesday,
        ["wed"] = DayOfWeek.Wednesday,
        ["thu"] = DayOfWeek.Thursday,
        ["fri"] = DayOfWeek.Friday,
        ["sat"] = DayOfWeek.Saturday,
        ["sun"] = DayOfWeek.Sunday,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    internal static ResourceSpec Read(YamlNode node, IReadOnlySet<string> serviceIds, int slotMinutes, string path)
    {
        if (node is not YamlMappingNode map)
        {
            throw new InvalidDataException($"{path}: cada recurso es un mapa");
        }

        var id = SetupYaml.RequiredText(map, "id", path);
        var offered = PlatformYaml.Child(map, "services") is YamlSequenceNode list
            ? list.Select(PlatformYaml.Text).Select(text => text ?? string.Empty).ToList()
            : [];
        var unknown = offered.Where(service => !serviceIds.Contains(service)).ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidDataException($"{path}: el recurso «{id}» ofrece servicios que no existen: {string.Join(", ", unknown)}");
        }

        var hours = new Dictionary<DayOfWeek, IReadOnlyList<TimeRange>>();
        if (PlatformYaml.Child(map, "hours") is YamlMappingNode days)
        {
            foreach (var (dayNode, rangesNode) in days.Children)
            {
                var dayName = PlatformYaml.Text(dayNode) ?? string.Empty;
                if (!_days.TryGetValue(dayName, out var day))
                {
                    throw new InvalidDataException($"{path}: día desconocido «{dayName}» en el recurso «{id}»");
                }

                hours[day] = ReadRanges(rangesNode, id, slotMinutes, path);
            }
        }

        return new ResourceSpec(id, SetupYaml.RequiredText(map, "label", path), SetupYaml.RequiredText(map, "place", path), offered, hours);
    }

    private static List<TimeRange> ReadRanges(YamlNode node, string id, int slotMinutes, string path)
    {
        var ranges = new List<TimeRange>();
        foreach (var pair in node as YamlSequenceNode ?? [])
        {
            if (pair is not YamlSequenceNode { Children.Count: 2 } bounds)
            {
                throw new InvalidDataException($"{path}: un tramo del recurso «{id}» no es un par de horas");
            }

            var start = SetupYaml.Time(bounds[0], path);
            var end = SetupYaml.Time(bounds[1], path);
            var minutes = (int)(end - start).TotalMinutes;
            if (end <= start)
            {
                throw new InvalidDataException($"{path}: un tramo está al revés en «{id}»: {start:HH\\:mm} a {end:HH\\:mm}");
            }

            if (minutes % slotMinutes != 0)
            {
                throw new InvalidDataException($"{path}: un tramo no es múltiplo de {slotMinutes} minutos en «{id}»: {start:HH\\:mm} a {end:HH\\:mm}");
            }

            ranges.Add(new TimeRange(start, end));
        }

        return ranges;
    }
}
