using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Storage;

namespace Aida.Web.Components.Pages.Agenda;

/// <summary>Lo que pinta la página tras una frase del asistente: qué día, qué panel, con qué relleno y qué mensajes.</summary>
/// <param name="Panel">El panel que se abre (<c>nueva</c>, <c>ficha</c> o <c>elegir</c>); <see langword="null"/> si la página
/// conserva el suyo y el día.</param>
/// <param name="Day">El día que se abre; el de hoy si la frase no dijo otro.</param>
/// <param name="ServiceId">El servicio del formulario de cita nueva, de los huecos o de las personas para elegir.</param>
/// <param name="Preferred">La hora que se marca en el hueco, si la frase dijo una y hay un hueco que la tenga.</param>
/// <param name="Prefill">El nombre y el teléfono de la cita nueva, si hay.</param>
/// <param name="Card">La ficha de la persona, para el panel <c>ficha</c>.</param>
/// <param name="Choices">Las personas que encajan, para el panel <c>elegir</c>.</param>
/// <param name="ChoicesForCard">Si las personas de <see cref="Choices"/> son para cancelar o mover (ficha) y no para una cita nueva.</param>
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
    IReadOnlyList<Client>? Choices,
    bool ChoicesForCard,
    string? MessageKey,
    string? NoteKey);
