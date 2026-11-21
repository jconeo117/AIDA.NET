using System.Globalization;

namespace Aida.Web.Components.Pages.Agenda;

/// <summary>Los enlaces de la agenda: la URL es lo que dice qué día y qué panel se ve.</summary>
/// <remarks>Una sola fuente para las direcciones, para que la rejilla, los paneles y la redirección tras cambiar algo
/// no escriban cada una su propia cadena de consulta.</remarks>
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
    /// <c>bloquear</c> o <c>bloqueo</c>.</param>
    /// <param name="value">Su argumento: la cita, el servicio para <c>nueva</c>, o el bloqueo para <c>bloqueo</c>.</param>
    /// <returns>La dirección.</returns>
    internal static string Panel(DateOnly day, string panel, string value) =>
        $"{Day(day)}&{panel}={Uri.EscapeDataString(value)}";

    /// <summary>A dónde se vuelve tras una acción hecha: a la cita o al bloqueo que quedó, o al día si ya no existe.</summary>
    /// <param name="day">El día de la acción.</param>
    /// <param name="action">La acción hecha.</param>
    /// <param name="data">Lo que devolvió la acción.</param>
    /// <returns>La dirección.</returns>
    /// <remarks>Al quitar un bloqueo su fila ya no existe, así que la vuelta es al día: no hay nada que abrir.</remarks>
    internal static string AfterDone(DateOnly day, string action, IReadOnlyDictionary<string, string> data)
    {
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

    /// <summary>El valor de un hueco en el formulario: <c>recurso|inicio</c>.</summary>
    /// <param name="resource">El recurso.</param>
    /// <param name="start">El inicio.</param>
    /// <returns>El valor.</returns>
    internal static string Slot(string resource, DateTimeOffset start) =>
        $"{resource}|{start.ToString(SlotFormat, CultureInfo.InvariantCulture)}";
}
