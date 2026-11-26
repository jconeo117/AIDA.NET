using System.Globalization;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;
using Aida.Platform.People;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Cambiar los datos de una cita sin mover su hora: la persona, el servicio o quién atiende.</summary>
/// <remarks>
/// <para>Es <c>_edit</c> de la 0.1 (<c>tool.py:225-271</c>), regla por regla, salvo el teléfono: ya no se rechaza el de otra
/// persona (N015, D5), porque el teléfono se comparte. Mover la hora es <c>reprogramar</c>. Un nombre o un teléfono que vienen
/// vacíos se rechazan; los que no vienen no se tocan. Se guardan como se escribieron, sin recortar, como en la 0.1.</para>
/// <para>El hueco se repasa solo si cambian el recurso o los minutos: con los mismos, la cita no ocupa nada nuevo. Cambiar
/// el teléfono de una cita cambia el de la persona, y con él el de todas sus citas.</para>
/// </remarks>
internal static class EditAction
{
    /// <summary>Aplica un cambio de datos a una cita agendada.</summary>
    /// <param name="store">El almacén de la petición, para las citas.</param>
    /// <param name="people">El almacén de las personas de la petición, para su nombre y su teléfono.</param>
    /// <param name="setup">La configuración: servicios y recursos que se pueden poner.</param>
    /// <param name="arguments">Los argumentos, con <c>by</c> de quien pide.</param>
    /// <returns>La cita hecha, o el primer rechazo de la lista en su orden.</returns>
    internal static ToolResult Run(AgendaStore store, PersonStore people, AgendaSetup setup, IReadOnlyDictionary<string, string> arguments)
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

        if (hasName || hasPhone)
        {
            var current = people.Find(appointment.ClientId) ?? throw new KeyNotFoundException($"no hay persona {appointment.ClientId}");
            people.UpdateContact(appointment.ClientId, hasName ? name! : current.Name, hasPhone ? phone! : current.Phone);
        }

        store.Update(appointment.Id, arguments["by"], resource: resourceId, service: service, minutes: minutes);
        return ActionArguments.AppointmentDone(appointment.Id);
    }

    /// <summary>Corregir el nombre, el teléfono y los datos opcionales de una persona, desde su ficha.</summary>
    /// <param name="people">El almacén de las personas de la petición.</param>
    /// <param name="catalog">Los tipos de documento y los sexos aceptados.</param>
    /// <param name="today">La fecha de hoy: una fecha de nacimiento no puede ser posterior.</param>
    /// <param name="arguments">Los argumentos: <c>client</c>, <c>name</c> y <c>phone</c>, y los opcionales de <see cref="ContactDetails"/>.</param>
    /// <returns>La persona hecha, o el primer rechazo en su orden.</returns>
    /// <remarks>Es <c>_edit_client</c> de la 0.1 (<c>tool.py:351-364</c>), con los datos de N015 (D6). A diferencia de
    /// <see cref="Run"/>, los dos campos se piden y se guardan recortados. Cambia la persona en todas sus citas, porque las
    /// citas apuntan a ella. El contacto y sus datos van en la misma transacción: no se guarda uno sin el otro.</remarks>
    internal static ToolResult Contact(PersonStore people, PersonCatalog catalog, DateOnly today, IReadOnlyDictionary<string, string> arguments)
    {
        if (!long.TryParse(ActionArguments.Text(arguments, "client"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            || people.Find(id) is not { } current)
        {
            return ToolResult.Rejected("unknown_client");
        }

        var name = ActionArguments.Text(arguments, "name").Trim();
        var phone = ActionArguments.Text(arguments, "phone").Trim();
        if (name.Length == 0 || phone.Length == 0)
        {
            return ToolResult.Rejected("missing_client");
        }

        if (ContactDetails.Merge(people, catalog, today, current, arguments, out var details) is { } detailsRejection)
        {
            return detailsRejection;
        }

        people.UpdateContact(current.Id, name, phone);
        people.UpdateDetails(current.Id, details);
        return ToolResult.Done(new Dictionary<string, string> { ["client"] = current.Id.ToString(CultureInfo.InvariantCulture) });
    }
}
