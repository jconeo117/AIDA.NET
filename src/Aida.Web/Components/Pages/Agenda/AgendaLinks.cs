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
    /// <param name="panel">El nombre del panel: <c>cita</c>, <c>mover</c>, <c>cancelar</c> o <c>nueva</c>.</param>
    /// <param name="value">Su argumento: la cita, o el servicio para <c>nueva</c>.</param>
    /// <returns>La dirección.</returns>
    internal static string Panel(DateOnly day, string panel, string value) =>
        $"{Day(day)}&{panel}={Uri.EscapeDataString(value)}";

    /// <summary>El valor de un hueco en el formulario: <c>recurso|inicio</c>.</summary>
    /// <param name="resource">El recurso.</param>
    /// <param name="start">El inicio.</param>
    /// <returns>El valor.</returns>
    internal static string Slot(string resource, DateTimeOffset start) =>
        $"{resource}|{start.ToString(SlotFormat, CultureInfo.InvariantCulture)}";
}
