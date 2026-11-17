namespace Aida.Web.Components.Pages.Agenda;

/// <summary>Lo que llega del formulario de la agenda: la acción pulsada y los campos que ella necesita.</summary>
/// <remarks>Un solo formulario para toda la página, para que ninguna acción llegue sin su autor ni se salte la puerta del
/// motor (ADR 0007). Los nombres de los campos (<c>Command.Action</c>, <c>Command.Slot</c>…) son los que usan los tests.</remarks>
public sealed class AgendaCommandForm
{
    /// <summary>La acción: <c>agendar</c>, <c>reprogramar</c>, <c>cancelar</c>, <c>atendida</c> o <c>no_asistio</c>.</summary>
    public string? Action { get; set; }

    /// <summary>La cita, para todas las acciones salvo <c>agendar</c>.</summary>
    public string? Appointment { get; set; }

    /// <summary>El hueco elegido, como <c>&lt;recurso&gt;|&lt;inicio&gt;</c>.</summary>
    public string? Slot { get; set; }

    /// <summary>El servicio, para <c>agendar</c>.</summary>
    public string? Service { get; set; }

    /// <summary>El nombre de quien viene, para <c>agendar</c>.</summary>
    public string? Name { get; set; }

    /// <summary>El teléfono de quien viene, para <c>agendar</c>.</summary>
    public string? Phone { get; set; }
}
