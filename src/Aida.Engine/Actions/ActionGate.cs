using Aida.Engine.Authorization;
using Aida.Engine.Events;
using Aida.Engine.Ontology;
using Aida.Engine.Serialization;

namespace Aida.Engine.Actions;

/// <summary>La puerta de acciones: la única forma de que una petición cambie algo fuera del motor.</summary>
/// <remarks>
/// <para>Es <c>ActionGate</c> de la 0.1 (<c>actions.py:49-97</c>): deja la petición en el registro antes de ejecutarla y
/// emite exactamente un evento de cierre, siempre. Si cada brazo cambiara sus datos por su cuenta, no habría un solo sitio
/// donde quedara escrito quién pidió qué ni cómo acabó.</para>
/// <para>Cambia una cosa (ADR 0035): compara la reversibilidad de la acción con el techo que le da la composición, y lo
/// que queda por encima se rechaza con <c>needs_authorization</c> sin llamar a la herramienta (ADR 0007). Proponer y
/// autorizar entran con la primera acción irreversible.</para>
/// </remarks>
public sealed class ActionGate
{
    private readonly ActionCatalog _catalog;
    private readonly Reversibility _ceiling;
    private readonly IToolPort _tool;
    private readonly EventLog _log;
    private readonly AgentId _owner;
    private readonly Func<long> _tick;

    /// <summary>Crea la puerta.</summary>
    /// <param name="catalog">Las acciones que existen y su reversibilidad.</param>
    /// <param name="ceiling">Hasta dónde se actúa sin permiso. Lo fija la composición, nunca el brazo.</param>
    /// <param name="tool">La herramienta del brazo.</param>
    /// <param name="log">El registro donde quedan la petición y su cierre.</param>
    /// <param name="owner">El dueño de los eventos: el brazo.</param>
    /// <param name="tick">Da el tick de cada evento; se llama una vez por evento, como en la 0.1.</param>
    public ActionGate(ActionCatalog catalog, Reversibility ceiling, IToolPort tool, EventLog log, AgentId owner, Func<long> tick)
    {
        _catalog = catalog;
        _ceiling = ceiling;
        _tool = tool;
        _log = log;
        _owner = owner;
        _tick = tick;
    }

    /// <summary>Deja la petición en el registro, la ejecuta si se puede y emite su cierre.</summary>
    /// <param name="request">La petición.</param>
    /// <returns>Cómo acabó.</returns>
    /// <exception cref="ArgumentException">Si la petición no tiene autor: no se registra nada (<c>I-156</c>).</exception>
    /// <remarks>La herramienta recibe la acción y sus argumentos ya ordenados, nunca la petición entera. Una excepción de la
    /// herramienta es un fallo con el nombre de su tipo, no un error de la puerta: el cierre se emite igual.</remarks>
    public ToolResult Submit(ActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(request.RequestedBy))
        {
            throw new ArgumentException("Una petición sin autor no se registra (I-156).", nameof(request));
        }

        var arguments = new SortedDictionary<string, string>(CodePointComparer.Instance);
        foreach (var (key, value) in request.Arguments)
        {
            arguments[key] = value;
        }

        var requested = new Dictionary<string, EventValue>(StringComparer.Ordinal) { ["by"] = EventValue.Of(request.RequestedBy) };
        foreach (var (key, value) in arguments)
        {
            requested[key] = EventValue.Of(value);
        }

        _log.Emit(_tick(), EventKind.ActionRequested, _owner, request.Action, requested);
        ToolResult result;
        if (!_catalog.TryGet(request.Action, out var reversibility))
        {
            result = ToolResult.Rejected("unknown_action");
        }
        else if (ReversibilityRank.Exceeds(reversibility, _ceiling))
        {
            result = ToolResult.Rejected("needs_authorization");
        }
        else
        {
            try
            {
                result = _tool.Execute(request.Action, arguments);
            }
            catch (Exception exception)
            {
                result = new ToolResult(ToolStatus.Failed, exception.GetType().Name);
            }
        }

        var closing = new Dictionary<string, EventValue>(StringComparer.Ordinal) { ["reason"] = EventValue.Of(result.Reason) };
        foreach (var (key, value) in result.Data)
        {
            closing[key] = EventValue.Of(value);
        }

        var kind = result.Status switch
        {
            ToolStatus.Done => EventKind.ActionCompleted,
            ToolStatus.Rejected => EventKind.ActionRejected,
            _ => EventKind.ActionFailed,
        };
        _log.Emit(_tick(), kind, _owner, request.Action, closing);
        return result;
    }
}
