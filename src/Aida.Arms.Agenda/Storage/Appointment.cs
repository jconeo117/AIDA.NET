namespace Aida.Arms.Agenda.Storage;

/// <summary>Una cita, tal como está guardada.</summary>
/// <remarks>Es <c>Appointment</c> de la 0.1 (<c>store.py:40-50</c>). La hora lleva su desfase: sin él no se sabe qué hora
/// es. Quién la creó y quién la cambió por última vez son identificadores de usuario (ADR 0006).</remarks>
/// <param name="Id">El identificador.</param>
/// <param name="ClientId">La persona que viene.</param>
/// <param name="Service">El servicio.</param>
/// <param name="Resource">Quién atiende.</param>
/// <param name="Start">Cuándo empieza.</param>
/// <param name="Minutes">Cuánto dura.</param>
/// <param name="State">En qué está.</param>
/// <param name="CreatedBy">Quién la agendó.</param>
/// <param name="UpdatedBy">Quién la cambió por última vez.</param>
public sealed record Appointment(long Id, long ClientId, string Service, string Resource, DateTimeOffset Start, int Minutes, AppointmentState State, string CreatedBy, string UpdatedBy)
{
    /// <summary>Cuándo acaba.</summary>
    public DateTimeOffset End => Start.AddMinutes(Minutes);
}
