namespace Aida.Arms.Agenda.Storage;

/// <summary>En qué está una cita.</summary>
/// <remarks>Los cuatro estados de la 0.1 (<c>store.py:32-36</c>). Solo una cita agendada se cambia: las demás están
/// cerradas.</remarks>
public enum AppointmentState
{
    /// <summary>Agendada.</summary>
    Booked,

    /// <summary>Cancelada: su hueco queda libre.</summary>
    Cancelled,

    /// <summary>Atendida.</summary>
    Attended,

    /// <summary>La persona no vino.</summary>
    NoShow,
}
