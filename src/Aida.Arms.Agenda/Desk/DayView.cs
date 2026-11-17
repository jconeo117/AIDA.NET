using Aida.Arms.Agenda.Setup;

namespace Aida.Arms.Agenda.Desk;

/// <summary>Lo que la pantalla necesita para pintar un día.</summary>
/// <remarks>Es lo que devolvía <c>GET dia/&lt;fecha&gt;</c> en la 0.1 (<c>app.py:268-338</c>) sin los bloqueos ni la semana, que
/// llegan en N009. Los huecos libres van por servicio, ya calculados con la misma regla que agendar.</remarks>
/// <param name="Day">El día.</param>
/// <param name="Now">La hora del servidor al pedirlo.</param>
/// <param name="Setup">La configuración: servicios, recursos y hueco.</param>
/// <param name="Appointments">Las citas que empiezan ese día, también las canceladas, en orden de inicio y de id.</param>
/// <param name="Free">Los huecos libres de cada servicio, en orden de inicio y de recurso.</param>
public sealed record DayView(DateOnly Day, DateTimeOffset Now, AgendaSetup Setup, IReadOnlyList<AppointmentView> Appointments, IReadOnlyDictionary<string, IReadOnlyList<FreeSlot>> Free);
