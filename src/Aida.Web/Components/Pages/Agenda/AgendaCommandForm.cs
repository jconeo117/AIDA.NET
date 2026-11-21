namespace Aida.Web.Components.Pages.Agenda;

/// <summary>Lo que llega del formulario de la agenda: la acción pulsada y los campos que ella necesita.</summary>
/// <remarks>Un solo formulario para toda la página, para que ninguna acción llegue sin su autor ni se salte la puerta del
/// motor (ADR 0007). Los nombres de los campos (<c>Command.Action</c>, <c>Command.Slot</c>…) son los que usan los tests.</remarks>
public sealed class AgendaCommandForm
{
    /// <summary>La acción: <c>agendar</c>, <c>editar</c>, <c>reprogramar</c>, <c>cancelar</c>, <c>atendida</c>, <c>no_asistio</c>,
    /// <c>bloquear</c>, <c>desbloquear</c> o <c>editar_contacto</c>.</summary>
    public string? Action { get; set; }

    /// <summary>La cita, para todas las acciones sobre una cita salvo <c>agendar</c>.</summary>
    public string? Appointment { get; set; }

    /// <summary>El hueco elegido, como <c>&lt;recurso&gt;|&lt;inicio&gt;</c>.</summary>
    public string? Slot { get; set; }

    /// <summary>El servicio, para <c>agendar</c> y <c>editar</c>.</summary>
    public string? Service { get; set; }

    /// <summary>El recurso que atiende, para <c>editar</c>, y el del bloqueo, para <c>bloquear</c>; vacío es «todos».</summary>
    public string? Resource { get; set; }

    /// <summary>El nombre de quien viene, para <c>agendar</c> y <c>editar</c>.</summary>
    public string? Name { get; set; }

    /// <summary>El teléfono de quien viene, para <c>agendar</c> y <c>editar</c>.</summary>
    public string? Phone { get; set; }

    /// <summary>El bloqueo que se quita, para <c>desbloquear</c>.</summary>
    public string? Block { get; set; }

    /// <summary>Desde cuándo, para <c>bloquear</c>, como lo escribe un <c>datetime-local</c>.</summary>
    public string? From { get; set; }

    /// <summary>Hasta cuándo, para <c>bloquear</c>, como lo escribe un <c>datetime-local</c>.</summary>
    public string? Until { get; set; }

    /// <summary>El motivo del bloqueo, para <c>bloquear</c>; solo lo ve el personal.</summary>
    public string? Reason { get; set; }

    /// <summary>El contacto, para <c>editar_contacto</c>: su id, que va en la URL de la ficha y en este campo.</summary>
    public string? Client { get; set; }
}
