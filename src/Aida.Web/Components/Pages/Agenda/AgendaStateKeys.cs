using Aida.Arms.Agenda.Storage;

namespace Aida.Web.Components.Pages.Agenda;

/// <summary>La clave del texto de cada estado de una cita, para la ficha, el detalle y la rejilla.</summary>
/// <remarks>Una sola tabla, para que el detalle de una cita y la ficha del contacto digan el mismo nombre de estado.</remarks>
internal static class AgendaStateKeys
{
    /// <summary>La clave del texto de un estado.</summary>
    /// <param name="state">El estado.</param>
    /// <returns>La clave en <c>ui.yaml</c>.</returns>
    internal static string Of(AppointmentState state) => state switch
    {
        AppointmentState.Booked => "state_booked",
        AppointmentState.Cancelled => "state_cancelled",
        AppointmentState.Attended => "state_attended",
        _ => "state_no_show",
    };
}
