using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Cambiar los datos de una cita sin mover su hora: la persona, el servicio o quién atiende.</summary>
/// <remarks>
/// <para>Es <c>_edit</c> de la 0.1 (<c>tool.py:225-271</c>), regla por regla. Mover la hora es <c>reprogramar</c>. Un nombre
/// o un teléfono que vienen vacíos se rechazan; los que no vienen no se tocan. Se guardan como se escribieron, sin recortar,
/// como en la 0.1.</para>
/// <para>El hueco se repasa solo si cambian el recurso o los minutos: con los mismos, la cita no ocupa nada nuevo. Cambiar
/// el teléfono de una cita cambia el del contacto, y con él el de todas sus citas.</para>
/// </remarks>
internal static class EditAction
{
    /// <summary>Aplica un cambio de datos a una cita agendada.</summary>
    /// <param name="store">El almacén de la petición.</param>
    /// <param name="setup">La configuración: servicios y recursos que se pueden poner.</param>
    /// <param name="arguments">Los argumentos, con <c>by</c> de quien pide.</param>
    /// <returns>La cita hecha, o el primer rechazo de la lista en su orden.</returns>
    internal static ToolResult Run(AgendaStore store, AgendaSetup setup, IReadOnlyDictionary<string, string> arguments)
    {
        if (ActionArguments.Booked(store, arguments, out var appointment) is { } rejection)
        {
            return rejection;
        }

        var hasName = arguments.TryGetValue("name", out var name);
        var hasPhone = arguments.TryGetValue("phone", out var phone);
        if ((hasName && name!.Trim().Length == 0) || (hasPhone && phone!.Trim().Length == 0))
        {
            return ToolResult.Rejected("missing_client");
        }

        var service = appointment.Service;
        var minutes = appointment.Minutes;
        var askedService = ActionArguments.Text(arguments, "service");
        if (askedService.Length > 0 && askedService != appointment.Service)
        {
            if (setup.Service(askedService) is not { } found)
            {
                return ToolResult.Rejected("unknown_service");
            }

            service = found.Id;
            minutes = found.Minutes;
        }

        var resourceId = appointment.Resource;
        var askedResource = ActionArguments.Text(arguments, "resource");
        if (askedResource.Length > 0 && askedResource != appointment.Resource)
        {
            if (setup.Resource(askedResource) is null)
            {
                return ToolResult.Rejected("unknown_resource");
            }

            resourceId = askedResource;
        }

        var resource = setup.Resource(resourceId);
        if (resource is null || !resource.Services.Contains(service))
        {
            return ToolResult.Rejected("service_not_offered");
        }

        if ((resourceId != appointment.Resource || minutes != appointment.Minutes)
            && SlotRules.Rejection(store, resource, appointment.Start, minutes, appointment.Id) is { } reason)
        {
            return ToolResult.Rejected(reason);
        }

        if (hasPhone && PhoneRejection(store, appointment.ClientId, phone!) is { } taken)
        {
            return ToolResult.Rejected(taken);
        }

        if (hasName || hasPhone)
        {
            var current = store.GetClient(appointment.ClientId);
            store.UpdateClient(appointment.ClientId, hasName ? name! : current.Name, hasPhone ? phone! : current.Phone);
        }

        store.Update(appointment.Id, arguments["by"], resource: resourceId, service: service, minutes: minutes);
        return ActionArguments.AppointmentDone(appointment.Id);
    }

    /// <summary>El teléfono es la llave del contacto: dos contactos no lo comparten (<c>tool.py:122-130</c>).</summary>
    /// <param name="store">El almacén de la petición.</param>
    /// <param name="clientId">El contacto al que pertenece la cita.</param>
    /// <param name="phone">El teléfono nuevo.</param>
    /// <returns><c>phone_taken</c>, o <see langword="null"/> si el teléfono es libre o ya era de ese contacto.</returns>
    /// <remarks>Sin esta regla, la base lo impide igual con su índice único, pero la puerta lo convertiría en un fallo y a
    /// quien pidió no se le explicaría nada. La reutiliza <c>editar_contacto</c> en N010.</remarks>
    internal static string? PhoneRejection(AgendaStore store, long clientId, string phone) =>
        store.ClientByPhone(phone) is { } other && other.Id != clientId ? "phone_taken" : null;
}
