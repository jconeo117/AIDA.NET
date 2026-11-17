using System.Globalization;

namespace Aida.Web.Components.Pages.Agenda;

/// <summary>Convierte lo que llegó del formulario en los argumentos de <c>AgendaDesk.Submit</c>.</summary>
/// <remarks>Solo pasan los campos que llegaron: lo que no vino no se inventa. El hueco se parte en recurso e inicio, sin
/// tocar el inicio; si no tiene la forma que exige la agenda, la herramienta lo rechaza con <c>bad_argument</c> y no lo
/// corrige la pantalla.</remarks>
internal static class AgendaArguments
{
    /// <summary>Los argumentos de la acción, sin el autor, que pone el mostrador.</summary>
    /// <param name="command">Lo que llegó del formulario.</param>
    /// <returns>Los argumentos.</returns>
    internal static Dictionary<string, string> From(AgendaCommandForm command)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(arguments, "appointment", command.Appointment);
        Add(arguments, "service", command.Service);
        Add(arguments, "name", command.Name);
        Add(arguments, "phone", command.Phone);
        if (!string.IsNullOrWhiteSpace(command.Slot))
        {
            var bar = command.Slot.IndexOf('|', StringComparison.Ordinal);
            if (bar < 0)
            {
                arguments["start"] = command.Slot;
            }
            else
            {
                arguments["resource"] = command.Slot[..bar];
                arguments["start"] = command.Slot[(bar + 1)..];
            }
        }

        return arguments;
    }

    /// <summary>El día de la cita que se acaba de hacer, para volver a verla.</summary>
    /// <param name="arguments">Los argumentos enviados.</param>
    /// <returns>El día del inicio, o <see langword="null"/> si la acción no trae inicio.</returns>
    internal static DateOnly? DayOf(IReadOnlyDictionary<string, string> arguments) =>
        arguments.TryGetValue("start", out var start) && start.Length >= 10
        && DateOnly.TryParseExact(start[..10], AgendaLinks.DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : null;

    private static void Add(Dictionary<string, string> arguments, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            arguments[key] = value.Trim();
        }
    }
}
