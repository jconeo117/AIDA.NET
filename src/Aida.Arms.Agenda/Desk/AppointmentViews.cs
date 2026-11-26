using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Platform.People;

namespace Aida.Arms.Agenda.Desk;

/// <summary>Arma la vista de una cita: el contacto, el servicio y los nombres de quien la tocó.</summary>
/// <remarks>Lo comparten el día, la ficha y los conflictos de un bloqueo, para que una cita se pinte igual en los tres sitios.</remarks>
internal static class AppointmentViews
{
    /// <summary>La vista de una cita.</summary>
    /// <param name="people">El almacén de las personas de la petición, para leer a quien viene.</param>
    /// <param name="setup">La configuración, para el nombre del servicio.</param>
    /// <param name="names">Los nombres de los usuarios, por identificador.</param>
    /// <param name="appointment">La cita.</param>
    /// <returns>La vista.</returns>
    internal static AppointmentView Of(PersonStore people, AgendaSetup setup, IReadOnlyDictionary<string, string> names, Appointment appointment)
    {
        var client = people.Find(appointment.ClientId) ?? throw new KeyNotFoundException($"no hay persona {appointment.ClientId}");
        return new AppointmentView(
            appointment,
            client.Name,
            client.Phone,
            setup.Service(appointment.Service)?.Label ?? appointment.Service,
            names.GetValueOrDefault(appointment.CreatedBy, appointment.CreatedBy),
            names.GetValueOrDefault(appointment.UpdatedBy, appointment.UpdatedBy));
    }
}
