using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;

namespace Aida.Arms.Agenda.Desk;

/// <summary>Arma la vista de una cita: el contacto, el servicio y los nombres de quien la tocó.</summary>
/// <remarks>Lo comparten el día, la ficha y los conflictos de un bloqueo, para que una cita se pinte igual en los tres sitios.</remarks>
internal static class AppointmentViews
{
    /// <summary>La vista de una cita.</summary>
    /// <param name="store">El almacén de la petición, para leer a la persona.</param>
    /// <param name="setup">La configuración, para el nombre del servicio.</param>
    /// <param name="names">Los nombres de los usuarios, por identificador.</param>
    /// <param name="appointment">La cita.</param>
    /// <returns>La vista.</returns>
    internal static AppointmentView Of(AgendaStore store, AgendaSetup setup, IReadOnlyDictionary<string, string> names, Appointment appointment)
    {
        var client = store.GetClient(appointment.ClientId);
        return new AppointmentView(
            appointment,
            client.Name,
            client.Phone,
            setup.Service(appointment.Service)?.Label ?? appointment.Service,
            names.GetValueOrDefault(appointment.CreatedBy, appointment.CreatedBy),
            names.GetValueOrDefault(appointment.UpdatedBy, appointment.UpdatedBy));
    }
}
