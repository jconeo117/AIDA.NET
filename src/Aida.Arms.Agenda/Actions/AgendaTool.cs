using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;
using Aida.Engine.Ontology;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Las reglas de cada acción de la agenda, detrás de la puerta del motor.</summary>
/// <remarks>
/// <para>Es <c>AgendaTool</c> de la 0.1 (<c>tool.py:133-378</c>) con ocho de sus nueve acciones; solo <c>editar_contacto</c>
/// llega con la ficha, en N010. No registra ni audita nada: eso es de la puerta y de <c>AgendaDesk</c>. Una regla que dice
/// que no se devuelve como rechazo, no se lanza.</para>
/// <para>El orden de las reglas importa, porque la primera que falla es la que se explica a quien pidió: es el de la 0.1,
/// regla por regla. Quién pide llega en el argumento <c>by</c>, que pone <c>AgendaDesk</c> a partir de la sesión.</para>
/// </remarks>
internal sealed class AgendaTool : IToolPort
{
    private readonly AgendaStore _store;
    private readonly AgendaSetup _setup;
    private readonly TimeProvider _time;

    internal AgendaTool(AgendaStore store, AgendaSetup setup, TimeProvider time)
    {
        _store = store;
        _setup = setup;
        _time = time;
    }

    /// <inheritdoc/>
    public ToolResult Execute(ActionId action, IReadOnlyDictionary<string, string> arguments) => action.Value switch
    {
        "agendar" => Book(arguments),
        "editar" => EditAction.Run(_store, _setup, arguments),
        "reprogramar" => Reschedule(arguments),
        "cancelar" => Close(arguments, AppointmentState.Cancelled, onlyAfterStart: false),
        "atendida" => Close(arguments, AppointmentState.Attended, onlyAfterStart: true),
        "no_asistio" => Close(arguments, AppointmentState.NoShow, onlyAfterStart: true),
        "bloquear" => BlockActions.Block(_store, _setup, _time.GetLocalNow(), arguments),
        "desbloquear" => BlockActions.Unblock(_store, arguments),
        _ => ToolResult.Rejected("unknown_action"),
    };

    private ToolResult Book(IReadOnlyDictionary<string, string> arguments)
    {
        var name = ActionArguments.Text(arguments, "name").Trim();
        var phone = ActionArguments.Text(arguments, "phone").Trim();
        if (name.Length == 0 || phone.Length == 0)
        {
            return ToolResult.Rejected("missing_client");
        }

        var service = _setup.Service(ActionArguments.Text(arguments, "service"));
        if (service is null)
        {
            return ToolResult.Rejected("unknown_service");
        }

        if (!StartArgument.TryParse(arguments.GetValueOrDefault("start"), out var start))
        {
            return ToolResult.Rejected("bad_argument");
        }

        var requested = ActionArguments.Text(arguments, "resource");
        if (requested.Length > 0 && _setup.Resource(requested) is null)
        {
            return ToolResult.Rejected("unknown_resource");
        }

        var candidates = _setup.Resources
            .Where(resource => (requested.Length == 0 || resource.Id == requested) && resource.Services.Contains(service.Id))
            .ToList();
        if (candidates.Count == 0)
        {
            return ToolResult.Rejected("service_not_offered");
        }

        if (start < _time.GetLocalNow())
        {
            return ToolResult.Rejected("past");
        }

        string? firstReason = null;
        foreach (var resource in candidates)
        {
            var reason = SlotRules.Rejection(_store, resource, start, service.Minutes, null);
            if (reason is null)
            {
                return Create(arguments["by"], name, phone, service, resource, start);
            }

            firstReason ??= reason;
        }

        return ToolResult.Rejected(firstReason ?? "outside_hours");
    }

    private ToolResult Create(string by, string name, string phone, ServiceSpec service, ResourceSpec resource, DateTimeOffset start)
    {
        var client = _store.ClientByPhone(phone);
        if (client is null)
        {
            client = _store.AddClient(name, phone);
        }
        else if (client.Name != name)
        {
            _store.UpdateClient(client.Id, name, phone);
        }

        return ActionArguments.AppointmentDone(_store.Add(client.Id, service.Id, resource.Id, start, service.Minutes, by).Id);
    }

    private ToolResult Reschedule(IReadOnlyDictionary<string, string> arguments)
    {
        if (ActionArguments.Booked(_store, arguments, out var appointment) is { } rejection)
        {
            return rejection;
        }

        if (!StartArgument.TryParse(arguments.GetValueOrDefault("start"), out var start))
        {
            return ToolResult.Rejected("bad_argument");
        }

        if (start < _time.GetLocalNow())
        {
            return ToolResult.Rejected("past");
        }

        var requested = ActionArguments.Text(arguments, "resource");
        if (requested.Length > 0 && _setup.Resource(requested) is null)
        {
            return ToolResult.Rejected("unknown_resource");
        }

        var resource = _setup.Resource(requested.Length > 0 ? requested : appointment.Resource);
        if (resource is null || !resource.Services.Contains(appointment.Service))
        {
            return ToolResult.Rejected("service_not_offered");
        }

        var reason = SlotRules.Rejection(_store, resource, start, appointment.Minutes, appointment.Id);
        if (reason is not null)
        {
            return ToolResult.Rejected(reason);
        }

        _store.Update(appointment.Id, arguments["by"], start: start, resource: requested.Length > 0 ? resource.Id : null);
        return ActionArguments.AppointmentDone(appointment.Id);
    }

    private ToolResult Close(IReadOnlyDictionary<string, string> arguments, AppointmentState state, bool onlyAfterStart)
    {
        if (ActionArguments.Booked(_store, arguments, out var appointment) is { } rejection)
        {
            return rejection;
        }

        if (onlyAfterStart && appointment.Start > _time.GetLocalNow())
        {
            return ToolResult.Rejected("not_yet");
        }

        _store.Update(appointment.Id, arguments["by"], state: state);
        return ActionArguments.AppointmentDone(appointment.Id);
    }
}
