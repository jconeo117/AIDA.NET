using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Si un hueco se puede ocupar: la única puerta de esta comprobación.</summary>
/// <remarks>Es <c>slot_rejection</c> de la 0.1 (<c>tool.py:65-119</c>), que usan la herramienta y los huecos libres: con dos
/// copias podrían decir cosas distintas. El orden es el de la 0.1 sin la regla de los bloqueos, que llega en N009: fuera de
/// horario y, después, solape.</remarks>
internal static class SlotRules
{
    internal static string? Rejection(AgendaStore store, ResourceSpec resource, DateTimeOffset start, int minutes, long? except)
    {
        if (!RangesCover(resource, start, minutes))
        {
            return "outside_hours";
        }

        foreach (var other in store.Between(start.AddDays(-1), start.AddDays(1)))
        {
            if (other.Id == except || other.Resource != resource.Id || other.State == AppointmentState.Cancelled)
            {
                continue;
            }

            if (Crosses(start, start.AddMinutes(minutes), other))
            {
                return "overlap";
            }
        }

        return null;
    }

    /// <summary>La cita entera cae dentro de un solo tramo del recurso para ese día (<c>tool.py:65-73</c>).</summary>
    internal static bool RangesCover(ResourceSpec resource, DateTimeOffset start, int minutes)
    {
        var end = start.AddMinutes(minutes);
        foreach (var range in resource.HoursOn(start.DayOfWeek))
        {
            var from = new DateTimeOffset(start.Date + range.Start.ToTimeSpan(), start.Offset);
            var until = new DateTimeOffset(start.Date + range.End.ToTimeSpan(), start.Offset);
            if (from <= start && end <= until)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Dos intervalos se cruzan si cada uno empieza antes de que acabe el otro: uno que acaba justo donde empieza
    /// otro no se cruza (<c>tool.py:76-79</c>).</summary>
    internal static bool Crosses(DateTimeOffset start, DateTimeOffset end, Appointment other) =>
        start < other.End && other.Start < end;
}
