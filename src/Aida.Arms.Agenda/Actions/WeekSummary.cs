using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Cuánto está ocupada cada día de una semana, en huecos.</summary>
/// <remarks>Es <c>week_summary</c> de la 0.1 (<c>tool.py:409-438</c>): de lunes a sábado. La capacidad son los pasos del
/// hueco en el horario de cada recurso que no tapa un bloqueo que le aplique; un tramo que no llega a un hueco entero cuenta
/// uno. Lo ocupado son las citas no canceladas que empiezan ese día, cada una en huecos redondeados hacia arriba. Los
/// bloqueos se juzgan con las mismas dos reglas que el hueco (<see cref="Storage.Block"/>).</remarks>
internal static class WeekSummary
{
    internal const int Days = 6;

    /// <summary>La semana que empieza en un lunes, día a día.</summary>
    /// <param name="store">El almacén de la petición.</param>
    /// <param name="setup">La configuración: horarios, recursos y hueco.</param>
    /// <param name="monday">El lunes de la semana.</param>
    /// <param name="offset">El desfase de la hora del servidor.</param>
    /// <returns>Seis días, de lunes a sábado.</returns>
    internal static IReadOnlyList<WeekDay> For(AgendaStore store, AgendaSetup setup, DateOnly monday, TimeSpan offset)
    {
        var slot = setup.SlotMinutes;
        var rows = new List<WeekDay>(Days);
        for (var shift = 0; shift < Days; shift++)
        {
            var day = monday.AddDays(shift);
            var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), offset);
            var blocks = store.BlocksBetween(dayStart, dayStart.AddDays(1));
            var capacity = 0;
            foreach (var resource in setup.Resources)
            {
                foreach (var range in resource.HoursOn(day.DayOfWeek))
                {
                    var cursor = new DateTimeOffset(day.ToDateTime(range.Start), offset);
                    var rangeEnd = new DateTimeOffset(day.ToDateTime(range.End), offset);
                    while (cursor < rangeEnd)
                    {
                        var next = cursor.AddMinutes(slot);
                        if (!blocks.Any(block => block.AppliesTo(resource.Id) && block.Crosses(cursor, next)))
                        {
                            capacity++;
                        }

                        cursor = next;
                    }
                }
            }

            var booked = store.Between(dayStart, dayStart.AddDays(1))
                .Where(appointment => appointment.State != AppointmentState.Cancelled)
                .Sum(appointment => (appointment.Minutes + slot - 1) / slot);
            rows.Add(new WeekDay(day, booked, capacity));
        }

        return rows;
    }
}
