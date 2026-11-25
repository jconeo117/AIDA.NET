using System.Globalization;

namespace Aida.Web.Components.Pages.Agenda;

/// <summary>Los enlaces de la agenda: la URL es lo que dice qué día y qué panel se ve.</summary>
/// <remarks>Una sola fuente para las direcciones, para que la rejilla, los paneles y la redirección tras cambiar algo
/// no escriban cada una su propia cadena de consulta. Lo que se busca nunca va en la URL: va en el formulario.</remarks>
internal static class AgendaLinks
{
    /// <summary>El formato del día en la URL.</summary>
    internal const string DayFormat = "yyyy-MM-dd";

    /// <summary>El formato del inicio de un hueco, el que acepta el mostrador.</summary>
    internal const string SlotFormat = "yyyy-MM-ddTHH:mm:sszzz";

    /// <summary>El día, sin panel.</summary>
    /// <param name="day">El día.</param>
    /// <returns>La dirección.</returns>
    internal static string Day(DateOnly day) => $"/agenda?dia={day.ToString(DayFormat, CultureInfo.InvariantCulture)}";

    /// <summary>El día con un panel abierto.</summary>
    /// <param name="day">El día.</param>
    /// <param name="panel">El nombre del panel: <c>cita</c>, <c>mover</c>, <c>cancelar</c>, <c>nueva</c>, <c>editar</c>,
    /// <c>contacto</c>, <c>ficha</c>, <c>bloquear</c> o <c>bloqueo</c>.</param>
    /// <param name="value">Su argumento: la cita, el servicio para <c>nueva</c>, el contacto para <c>ficha</c> y
    /// <c>contacto</c>, o el bloqueo para <c>bloqueo</c>.</param>
    /// <returns>La dirección.</returns>
    internal static string Panel(DateOnly day, string panel, string value) =>
        $"{Day(day)}&{panel}={Uri.EscapeDataString(value)}";

    /// <summary>La nueva cita, ya con los datos de la persona de su ficha.</summary>
    /// <param name="day">El día.</param>
    /// <param name="service">El servicio con el que se abre el formulario.</param>
    /// <param name="client">El contacto, cuyo nombre y teléfono rellena el servidor.</param>
    /// <returns>La dirección.</returns>
    internal static string NewFor(DateOnly day, string service, long client) =>
        $"{Panel(day, "nueva", service)}&contacto={client.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>A dónde se vuelve tras una acción hecha: a la ficha, a la cita o al bloqueo que quedó, o al día si ya no existe.</summary>
    /// <param name="day">El día de la acción.</param>
    /// <param name="action">La acción hecha.</param>
    /// <param name="data">Lo que devolvió la acción.</param>
    /// <returns>La dirección.</returns>
    /// <remarks>Al quitar un bloqueo su fila ya no existe, así que la vuelta es al día: no hay nada que abrir. El contacto va
    /// antes que la cita porque editar un contacto no deja ninguna cita que abrir.</remarks>
    internal static string AfterDone(DateOnly day, string action, IReadOnlyDictionary<string, string> data)
    {
        if (data.TryGetValue("client", out var client))
        {
            return Panel(day, "ficha", client);
        }

        if (data.TryGetValue("appointment", out var appointment))
        {
            return Panel(day, "cita", appointment);
        }

        if (action == "bloquear" && data.TryGetValue("block", out var block))
        {
            return Panel(day, "bloqueo", block);
        }

        return Day(day);
    }

    /// <summary>Añade la hora preferida a un enlace, para que el hueco de esa hora salga marcado.</summary>
    /// <param name="link">El enlace, que ya lleva el día y el panel.</param>
    /// <param name="time">La hora, si la frase la dijo y hay un hueco que la tiene.</param>
    /// <returns>El enlace con <c>&amp;hora=HH:mm</c>, o el mismo enlace si no hay hora.</returns>
    /// <remarks>La hora es lo único que de la frase viaja en la dirección, y solo como <c>HH:mm</c>: lo tecleado nunca va en la URL.</remarks>
    internal static string WithTime(string link, TimeOnly? time) =>
        time is { } hour ? $"{link}&hora={hour.ToString("HH:mm", CultureInfo.InvariantCulture)}" : link;

    /// <summary>El valor de un hueco en el formulario: <c>recurso|inicio</c>.</summary>
    /// <param name="resource">El recurso.</param>
    /// <param name="start">El inicio.</param>
    /// <returns>El valor.</returns>
    internal static string Slot(string resource, DateTimeOffset start) =>
        $"{resource}|{start.ToString(SlotFormat, CultureInfo.InvariantCulture)}";
}
