using System.Globalization;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Bloquear un tramo de la agenda y quitar un bloqueo.</summary>
/// <remarks>
/// <para>Son <c>_block</c> y <c>_unblock</c> de la 0.1 (<c>tool.py:310-349</c>), regla por regla. Un bloqueo con citas
/// agendadas dentro se rechaza y las nombra: primero se reprograman o se cancelan, para que nadie llegue a una consulta
/// cerrada. Las citas se buscan desde un día antes del inicio, porque una que empezó la víspera puede seguir dentro.</para>
/// <para>Quitar un bloqueo borra su fila; el hecho queda en la auditoría.</para>
/// </remarks>
internal static class BlockActions
{
    /// <summary>Guarda un bloqueo, si no le estorban citas agendadas.</summary>
    /// <param name="store">El almacén de la petición.</param>
    /// <param name="setup">La configuración: el recurso, si se nombra, tiene que existir.</param>
    /// <param name="now">La hora del servidor: un bloqueo que acaba antes o ahora ya pasó.</param>
    /// <param name="arguments">Los argumentos: <c>start</c>, <c>end</c>, <c>resource</c>, <c>reason</c> y <c>by</c>.</param>
    /// <returns>El bloqueo hecho, o el primer rechazo en su orden.</returns>
    internal static ToolResult Block(AgendaStore store, AgendaSetup setup, DateTimeOffset now, IReadOnlyDictionary<string, string> arguments)
    {
        if (!StartArgument.TryParse(arguments.GetValueOrDefault("start"), out var start)
            || !StartArgument.TryParse(arguments.GetValueOrDefault("end"), out var end)
            || end <= start)
        {
            return ToolResult.Rejected("bad_argument");
        }

        var resource = ActionArguments.Text(arguments, "resource");
        if (resource.Length > 0 && setup.Resource(resource) is null)
        {
            return ToolResult.Rejected("unknown_resource");
        }

        if (end <= now)
        {
            return ToolResult.Rejected("past");
        }

        var inside = store.Between(start.AddDays(-1), end)
            .Where(appointment => appointment.State == AppointmentState.Booked
                && (resource.Length == 0 || appointment.Resource == resource)
                && SlotRules.Crosses(start, end, appointment))
            .Select(appointment => appointment.Id.ToString(CultureInfo.InvariantCulture))
            .ToList();
        if (inside.Count > 0)
        {
            return ToolResult.Rejected("has_appointments", new Dictionary<string, string> { ["appointments"] = string.Join(',', inside) });
        }

        var block = store.AddBlock(resource, start, end, ActionArguments.Text(arguments, "reason").Trim(), arguments["by"]);
        return ToolResult.Done(new Dictionary<string, string> { ["block"] = block.Id.ToString(CultureInfo.InvariantCulture) });
    }

    /// <summary>Quita un bloqueo por su identificador.</summary>
    /// <param name="store">El almacén de la petición.</param>
    /// <param name="arguments">Los argumentos: <c>block</c>.</param>
    /// <returns>La acción hecha, o <c>unknown_block</c> si no existe.</returns>
    internal static ToolResult Unblock(AgendaStore store, IReadOnlyDictionary<string, string> arguments)
    {
        if (!long.TryParse(ActionArguments.Text(arguments, "block"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            || !store.RemoveBlock(id))
        {
            return ToolResult.Rejected("unknown_block");
        }

        return ToolResult.Done(new Dictionary<string, string> { ["block"] = id.ToString(CultureInfo.InvariantCulture) });
    }
}
