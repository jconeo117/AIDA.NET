using Aida.Arms.Agenda.Storage;
using Aida.Platform.People;

namespace Aida.Arms.Agenda.Desk;

/// <summary>La ficha de un contacto: sus datos, sus cifras y sus citas.</summary>
/// <remarks>Es <c>_card</c> de la 0.1 (<c>app.py:340-373</c>), sin el teléfono de la conversación, que llega con el canal
/// (M6). Las cifras se cuentan en <see cref="Build"/> y en ningún otro sitio, como en la 0.1.</remarks>
/// <param name="Client">La persona, como la ve la agenda.</param>
/// <param name="Details">Su documento, su fecha de nacimiento y su sexo, si se conocen.</param>
/// <param name="Upcoming">Las citas agendadas que todavía no han empezado.</param>
/// <param name="Attended">Las atendidas.</param>
/// <param name="NoShow">Las de no asistió.</param>
/// <param name="Appointments">Todas sus citas, de la más reciente a la más antigua.</param>
public sealed record ClientCard(Client Client, PersonDetails Details, int Upcoming, int Attended, int NoShow, IReadOnlyList<AppointmentView> Appointments)
{
    /// <summary>Cuenta las cifras de una ficha.</summary>
    /// <param name="person">La persona.</param>
    /// <param name="oldestFirst">Sus citas, de la más antigua a la más reciente.</param>
    /// <param name="now">La hora del servidor: una cita agendada que empieza en este instante ya no es próxima.</param>
    /// <returns>La ficha.</returns>
    internal static ClientCard Build(Person person, IReadOnlyList<AppointmentView> oldestFirst, DateTimeOffset now) => new(
        Client.From(person),
        person.Details,
        oldestFirst.Count(view => view.Appointment.State == AppointmentState.Booked && view.Appointment.Start >= now),
        oldestFirst.Count(view => view.Appointment.State == AppointmentState.Attended),
        oldestFirst.Count(view => view.Appointment.State == AppointmentState.NoShow),
        [.. oldestFirst.Reverse()]);
}
