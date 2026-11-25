using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Storage;
using Aida.Platform.Assistant;

namespace Aida.Arms.Agenda.Assistant;

/// <summary>Busca las citas de unas personas que encajan con el origen de una frase.</summary>
/// <remarks>
/// Solo cuentan las citas agendadas que empiezan ahora o después (D25). El origen se compara con el día de la semana, el día
/// o la hora que se dijo: «la del martes» es el martes que viene aunque el día ya sea otro (D13), y «a las 3» encaja la cita a
/// las 03:00 o a las 15:00 (D26). Así un operador no tiene que buscar la cita: la frase la encuentra.
/// </remarks>
internal sealed class AppointmentResolver
{
    private readonly AgendaDesk _desk;

    /// <summary>Crea el buscador.</summary>
    /// <param name="desk">El mostrador, para leer la ficha de cada persona.</param>
    internal AppointmentResolver(AgendaDesk desk)
    {
        _desk = desk;
    }

    /// <summary>Las citas que encajan, de todas las personas juntas.</summary>
    /// <param name="clients">Las personas que encajan.</param>
    /// <param name="origin">El momento de origen, o <see langword="null"/> si no se dijo ninguno: entonces cuentan todas.</param>
    /// <param name="now">La hora del servidor: las citas que ya empezaron no cuentan.</param>
    /// <returns>Las citas, por inicio y después por id.</returns>
    internal IReadOnlyList<AppointmentView> Find(IReadOnlyList<Client> clients, WhenMention? origin, DateTimeOffset now) =>
        [.. clients
            .SelectMany(client => _desk.Card(client.Id)?.Appointments ?? [])
            .Where(view => view.Appointment.State == AppointmentState.Booked && view.Appointment.Start >= now)
            .Where(view => origin is null || Matches(view.Appointment.Start, origin))
            .OrderBy(view => view.Appointment.Start)
            .ThenBy(view => view.Appointment.Id)];

    private static bool Matches(DateTimeOffset start, WhenMention origin)
    {
        if (origin.Weekday is { } weekday && start.DayOfWeek != weekday)
        {
            return false;
        }

        if (origin.Weekday is null && origin.Day is { } day && DateOnly.FromDateTime(start.DateTime) != day)
        {
            return false;
        }

        var local = TimeOnly.FromDateTime(start.DateTime);
        return origin.Time is { } time
            ? MatchesTime(local, time, origin.Period)
            : origin.Period == DayPeriod.Unknown || InPeriod(local, origin.Period);
    }

    // Sin parte del día y antes de las 12, la hora dicha encaja a esa hora o doce horas después (D26).
    private static bool MatchesTime(TimeOnly local, TimeOnly said, DayPeriod period) =>
        period == DayPeriod.Unknown && said.Hour < 12
            ? local == said || local == said.AddHours(12)
            : local == said;

    private static bool InPeriod(TimeOnly local, DayPeriod period) =>
        period == DayPeriod.Morning ? local < new TimeOnly(12, 0) : local >= new TimeOnly(12, 0);
}
