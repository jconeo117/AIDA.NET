using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Los huecos en los que agendar un servicio saldría bien.</summary>
/// <remarks>Es <c>free_slots</c> de la 0.1 (<c>tool.py:381-406</c>): recorre el horario de cada recurso que da el servicio,
/// en pasos del hueco, y se queda con lo que <see cref="SlotRules"/> acepta y no ha pasado. Usa la misma regla que la
/// herramienta, así que un hueco que se ofrece no se rechaza al agendarlo.</remarks>
internal static class FreeSlots
{
    internal static IReadOnlyList<FreeSlot> For(AgendaStore store, AgendaSetup setup, DateOnly day, string serviceId, DateTimeOffset now)
    {
        var service = setup.Service(serviceId);
        if (service is null)
        {
            return [];
        }

        var slots = new List<FreeSlot>();
        foreach (var resource in setup.Resources)
        {
            if (!resource.Services.Contains(serviceId))
            {
                continue;
            }

            foreach (var range in resource.HoursOn(day.DayOfWeek))
            {
                var cursor = new DateTimeOffset(day.ToDateTime(range.Start), now.Offset);
                var rangeEnd = new DateTimeOffset(day.ToDateTime(range.End), now.Offset);
                while (cursor.AddMinutes(service.Minutes) <= rangeEnd)
                {
                    if (cursor >= now && SlotRules.Rejection(store, resource, cursor, service.Minutes, null) is null)
                    {
                        slots.Add(new FreeSlot(resource.Id, cursor));
                    }

                    cursor = cursor.AddMinutes(setup.SlotMinutes);
                }
            }
        }

        return [.. slots.OrderBy(slot => slot.Start).ThenBy(slot => slot.Resource, StringComparer.Ordinal)];
    }
}
