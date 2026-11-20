using System.Globalization;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Lo que comparten las acciones de la agenda: leer un argumento, encontrar la cita y decir que se hizo.</summary>
/// <remarks>Un solo sitio, para que todas las acciones que cambian una cita empiecen igual (<c>tool.py:366-378</c>).</remarks>
internal static class ActionArguments
{
    /// <summary>El argumento, o vacío si no vino.</summary>
    /// <param name="arguments">Los argumentos de la acción.</param>
    /// <param name="key">El nombre del argumento.</param>
    /// <returns>El valor tal como llegó, sin recortar.</returns>
    internal static string Text(IReadOnlyDictionary<string, string> arguments, string key) =>
        arguments.TryGetValue(key, out var value) ? value : string.Empty;

    /// <summary>La cita del argumento <c>appointment</c>, si existe y sigue agendada.</summary>
    /// <param name="store">El almacén de la petición.</param>
    /// <param name="arguments">Los argumentos de la acción.</param>
    /// <param name="appointment">La cita encontrada; si el rechazo es la respuesta, no se usa.</param>
    /// <returns>El rechazo (<c>unknown_appointment</c>, <c>not_booked</c>), o <see langword="null"/> si se puede cambiar.</returns>
    internal static ToolResult? Booked(AgendaStore store, IReadOnlyDictionary<string, string> arguments, out Appointment appointment)
    {
        appointment = null!;
        if (!long.TryParse(Text(arguments, "appointment"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            || store.Get(id) is not { } found)
        {
            return ToolResult.Rejected("unknown_appointment");
        }

        appointment = found;
        return found.State == AppointmentState.Booked ? null : ToolResult.Rejected("not_booked");
    }

    /// <summary>Una acción sobre una cita, hecha: devuelve su id en <c>appointment</c>.</summary>
    /// <param name="appointment">El identificador de la cita.</param>
    /// <returns>La acción hecha, con el identificador.</returns>
    internal static ToolResult AppointmentDone(long appointment) =>
        ToolResult.Done(new Dictionary<string, string> { ["appointment"] = appointment.ToString(CultureInfo.InvariantCulture) });
}
