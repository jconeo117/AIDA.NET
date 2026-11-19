using System.Globalization;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;
using Aida.Engine.Ontology;

namespace Aida.Arms.Agenda.Actions;

/// <summary>Las reglas de cada acción de la agenda, detrás de la puerta del motor.</summary>
/// <remarks>
/// <para>Es <c>AgendaTool</c> de la 0.1 (<c>tool.py:133-378</c>) con cinco de sus nueve acciones; editar, los bloqueos y la
/// ficha llegan en N009. No registra ni audita nada: eso es de la puerta y de <c>AgendaDesk</c>. Una regla que dice que
/// no se devuelve como rechazo, no se lanza.</para>
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
        "reprogramar" => Reschedule(arguments),
        "cancelar" => Close(arguments, AppointmentState.Cancelled, onlyAfterStart: false),
        "atendida" => Close(arguments, AppointmentState.Attended, onlyAfterStart: true),
        "no_asistio" => Close(arguments, AppointmentState.NoShow, onlyAfterStart: true),
        _ => ToolResult.Rejected("unknown_action"),
    };

    private static ToolResult Done(long appointment) =>
        ToolResult.Done(new Dictionary<string, string> { ["appointment"] = appointment.ToString(CultureInfo.InvariantCulture) });

    private static string Get(IReadOnlyDictionary<string, string> arguments, string key) =>
        arguments.TryGetValue(key, out var value) ? value : string.Empty;

    private ToolResult Book(IReadOnlyDictionary<string, string> arguments)
    {
        var name = Get(arguments, "name").Trim();
        var phone = Get(arguments, "phone").Trim();
        if (name.Length == 0 || phone.Length == 0)
        {
            return ToolResult.Rejected("missing_client");
        }

        var service = _setup.Service(Get(arguments, "service"));
        if (service is null)
        {
            return ToolResult.Rejected("unknown_service");
        }

        if (!StartArgument.TryParse(arguments.GetValueOrDefault("start"), out var start))
        {
            return ToolResult.Rejected("bad_argument");
        }

        var requested = Get(arguments, "resource");
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

        return Done(_store.Add(client.Id, service.Id, resource.Id, start, service.Minutes, by).Id);
    }

    private ToolResult Reschedule(IReadOnlyDictionary<string, string> arguments)
    {
        if (Booked(arguments, out var appointment) is { } rejection)
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

        var requested = Get(arguments, "resource");
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
        return Done(appointment.Id);
    }

    private ToolResult Close(IReadOnlyDictionary<string, string> arguments, AppointmentState state, bool onlyAfterStart)
    {
        if (Booked(arguments, out var appointment) is { } rejection)
        {
            return rejection;
        }

        if (onlyAfterStart && appointment.Start > _time.GetLocalNow())
        {
            return ToolResult.Rejected("not_yet");
        }

        _store.Update(appointment.Id, arguments["by"], state: state);
        return Done(appointment.Id);
    }

    /// <summary>La cita del argumento, si existe y sigue agendada (<c>tool.py:366-378</c>).</summary>
    /// <returns>El rechazo, o <see langword="null"/> si la cita se puede cambiar.</returns>
    private ToolResult? Booked(IReadOnlyDictionary<string, string> arguments, out Appointment appointment)
    {
        appointment = null!;
        if (!long.TryParse(Get(arguments, "appointment"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            || _store.Get(id) is not { } found)
        {
            return ToolResult.Rejected("unknown_appointment");
        }

        appointment = found;
        return found.State == AppointmentState.Booked ? null : ToolResult.Rejected("not_booked");
    }
}
