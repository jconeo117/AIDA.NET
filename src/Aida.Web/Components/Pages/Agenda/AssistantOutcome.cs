using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Storage;

namespace Aida.Web.Components.Pages.Agenda;

/// <summary>Lo que pinta la página tras una frase del asistente: qué día, qué panel, con qué relleno y qué mensajes.</summary>
/// <param name="Panel">El panel que se abre (<c>nueva</c>, <c>ficha</c>, <c>elegir</c>, <c>cancelar</c> o <c>mover</c>);
/// <see langword="null"/> si la página conserva el suyo y el día.</param>
/// <param name="Day">El día que se abre; el de hoy si la frase no dijo otro.</param>
/// <param name="ServiceId">El servicio del formulario de cita nueva, de los huecos o de las personas para elegir.</param>
/// <param name="Preferred">La hora que se marca en el hueco, si la frase dijo una y hay un hueco que la tenga.</param>
/// <param name="Prefill">El nombre y el teléfono de la cita nueva, si hay.</param>
/// <param name="Card">La ficha de la persona, para el panel <c>ficha</c>.</param>
/// <param name="Appointment">La cita que se cancela o se mueve.</param>
/// <param name="Choices">Las personas que encajan, para elegir una cita nueva.</param>
/// <param name="AppointmentChoices">Las citas que encajan, para elegir una a cancelar o mover.</param>
/// <param name="ChoicesToMove">Si las citas para elegir son para mover y no para cancelar.</param>
/// <param name="MoveTo">El día de destino al mover, si se dijo.</param>
/// <param name="MessageKey">La clave del mensaje de la página, si lo hay.</param>
/// <param name="NoteKey">La clave de la nota sobre lo que se pidió y no se pudo dar, si la hay.</param>
/// <remarks>Lo calcula <see cref="AgendaAssistantView"/> y lo aplica la página: así la vista no toca el estado de la página, y
/// el mapa de propuestas a paneles vive en un solo sitio.</remarks>
internal sealed record AssistantOutcome(
    string? Panel,
    DateOnly Day,
    string? ServiceId,
    TimeOnly? Preferred,
    Client? Prefill,
    ClientCard? Card,
    AppointmentView? Appointment,
    IReadOnlyList<Client>? Choices,
    IReadOnlyList<AppointmentView>? AppointmentChoices,
    bool ChoicesToMove,
    DateOnly? MoveTo,
    string? MessageKey,
    string? NoteKey);
