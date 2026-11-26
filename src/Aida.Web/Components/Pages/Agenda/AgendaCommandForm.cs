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

    /// <summary>La persona: para <c>editar_contacto</c>, su id; para <c>agendar</c>, su id, o <c>new</c> para una persona nueva.</summary>
    public string? Client { get; set; }

    /// <summary>El tipo de documento, para <c>editar_contacto</c>. Vacío lo borra; si el formulario no lo trae, no se toca.</summary>
    public string? DocumentType { get; set; }

    /// <summary>El número de documento, para <c>editar_contacto</c>. Va con el tipo: los dos o ninguno.</summary>
    public string? DocumentNumber { get; set; }

    /// <summary>La fecha de nacimiento, para <c>editar_contacto</c>, como <c>yyyy-MM-dd</c>. Vacía la borra.</summary>
    public string? BirthDate { get; set; }

    /// <summary>El sexo, para <c>editar_contacto</c>. Vacío lo borra.</summary>
    public string? Sex { get; set; }
}
