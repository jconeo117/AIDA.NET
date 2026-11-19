using Aida.Arms.Agenda.Storage;

namespace Aida.Arms.Agenda.Desk;

/// <summary>Una cita como la pinta la pantalla: con el nombre de la persona, del servicio y de quien la tocó.</summary>
/// <remarks>Los campos de cada cita de <c>_day</c> en la 0.1 (<c>app.py:276-293</c>). Los nombres se resuelven aquí, en un
/// solo sitio, y no en cada página.</remarks>
/// <param name="Appointment">La cita.</param>
/// <param name="ClientName">El nombre de quien viene.</param>
/// <param name="ClientPhone">Su teléfono.</param>
/// <param name="ServiceLabel">El nombre del servicio, o su identificador si ya no está en la configuración.</param>
/// <param name="CreatedByName">El nombre de quien la agendó, o su identificador si ya no existe.</param>
/// <param name="UpdatedByName">El nombre de quien la cambió por última vez.</param>
public sealed record AppointmentView(Appointment Appointment, string ClientName, string ClientPhone, string ServiceLabel, string CreatedByName, string UpdatedByName);
