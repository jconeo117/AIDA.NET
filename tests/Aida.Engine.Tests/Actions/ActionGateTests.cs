using Aida.Engine.Actions;
using Aida.Engine.Events;
using Aida.Engine.Ontology;
using Xunit;

namespace Aida.Engine.Tests.Actions;

public sealed class ActionGateTests
{
    private sealed class SpyTool : IToolPort
    {
        public List<(ActionId Action, IReadOnlyDictionary<string, string> Arguments)> Calls { get; } = [];

        public ToolResult Result { get; set; } = ToolResult.Done(new Dictionary<string, string> { ["appointment"] = "1" });

        public Exception? Exception { get; set; }

        public EventLog? Log { get; set; }

        public int EventsBeforeExecution { get; set; }

        public ToolResult Execute(ActionId action, IReadOnlyDictionary<string, string> arguments)
        {
            if (Log is not null)
            {
                EventsBeforeExecution = Log.Count();
            }

            Calls.Add((action, arguments));
            if (Exception is not null)
            {
                throw Exception;
            }

            return Result;
        }
    }

    private static (ActionGate Gate, EventLog Log, Func<long> Tick) CreateGate(
        IToolPort tool,
        ActionCatalog? catalog = null,
        Reversibility ceiling = Reversibility.Reversible)
    {
        var log = new EventLog();
        if (tool is SpyTool spy)
        {
            spy.Log = log;
        }

        long tickCounter = 0;
        long NextTick() => ++tickCounter;

        var cat = catalog ?? ActionCatalog.Parse([
            KeyValuePair.Create("agendar", "reversible"),
            KeyValuePair.Create("cancelar", "reversible"),
        ], "test");

        var gate = new ActionGate(cat, ceiling, tool, log, new AgentId("agenda"), NextTick);
        return (gate, log, NextTick);
    }

    [Fact]
    public void Emite_requested_y_completed_en_orden()
    {
        var spy = new SpyTool { Result = ToolResult.Done(new Dictionary<string, string> { ["appointment"] = "42" }) };
        var (gate, log, _) = CreateGate(spy);

        var req = new ActionRequest(
            new ActionId("agendar"),
            new Dictionary<string, string>
            {
                ["phone"] = "3001234567",
                ["name"] = "Ana",
                ["service"] = "consulta",
            },
            "prof-1");

        var res = gate.Submit(req);

        Assert.Equal(ToolStatus.Done, res.Status);
        Assert.Single(spy.Calls);
        Assert.Equal(1, spy.EventsBeforeExecution);

        var events = log.ToList();
        Assert.Equal(2, events.Count);

        var eReq = events[0];
        Assert.Equal(EventKind.ActionRequested, eReq.Kind);
        Assert.Equal(new ActionId("agendar"), eReq.Action);
        Assert.NotNull(eReq.Data);
        Assert.Equal(EventValue.Of("prof-1"), eReq.Data["by"]);
        Assert.Equal(EventValue.Of("Ana"), eReq.Data["name"]);
        Assert.Equal(EventValue.Of("3001234567"), eReq.Data["phone"]);
        Assert.Equal(EventValue.Of("consulta"), eReq.Data["service"]);

        var eClose = events[1];
        Assert.Equal(EventKind.ActionCompleted, eClose.Kind);
        Assert.Equal(new ActionId("agendar"), eClose.Action);
        Assert.NotNull(eClose.Data);
        Assert.Equal(EventValue.Of(string.Empty), eClose.Data["reason"]);
        Assert.Equal(EventValue.Of("42"), eClose.Data["appointment"]);
    }

    [Fact]
    public void Accion_desconocida_rechaza_sin_llamar_a_la_herramienta()
    {
        var spy = new SpyTool();
        var catalog = ActionCatalog.Parse([KeyValuePair.Create("agendar", "reversible")], "test");
        var (gate, log, _) = CreateGate(spy, catalog);

        var req = new ActionRequest(
            new ActionId("accion_inexistente"),
            new Dictionary<string, string> { ["foo"] = "bar" },
            "prof-1");

        var res = gate.Submit(req);

        Assert.Equal(ToolStatus.Rejected, res.Status);
        Assert.Equal("unknown_action", res.Reason);
        Assert.Empty(spy.Calls);

        var events = log.ToList();
        Assert.Equal(2, events.Count);
        Assert.Equal(EventKind.ActionRequested, events[0].Kind);
        Assert.Equal(EventKind.ActionRejected, events[1].Kind);
        Assert.Equal(EventValue.Of("unknown_action"), events[1].Data!["reason"]);
    }

    [Fact]
    public void Una_excepcion_de_la_herramienta_es_failed_con_su_tipo_y_sigue()
    {
        var spy = new SpyTool { Exception = new InvalidOperationException("fallo de disco simulado") };
        var (gate, log, _) = CreateGate(spy);

        var req = new ActionRequest(
            new ActionId("agendar"),
            new Dictionary<string, string> { ["name"] = "Carlos" },
            "prof-1");

        var res = gate.Submit(req);

        Assert.Equal(ToolStatus.Failed, res.Status);
        Assert.Equal("InvalidOperationException", res.Reason);

        var events = log.ToList();
        Assert.Equal(2, events.Count);
        Assert.Equal(EventKind.ActionRequested, events[0].Kind);
        Assert.Equal(EventKind.ActionFailed, events[1].Kind);
        Assert.Equal(EventValue.Of("InvalidOperationException"), events[1].Data!["reason"]);

        // La puerta sigue operativa tras la excepción
        spy.Exception = null;
        spy.Result = ToolResult.Done(new Dictionary<string, string>());
        var res2 = gate.Submit(req);
        Assert.Equal(ToolStatus.Done, res2.Status);
    }

    [Fact]
    public void Sin_autor_lanza_y_no_registra_nada()
    {
        var spy = new SpyTool();
        var (gate, log, _) = CreateGate(spy);

        var req = new ActionRequest(
            new ActionId("agendar"),
            new Dictionary<string, string> { ["name"] = "Sin Autor" },
            string.Empty);

        Assert.Throws<ArgumentException>(() => gate.Submit(req));
        Assert.Empty(spy.Calls);
        Assert.Empty(log);
    }

    [Fact]
    public void Los_argumentos_llegan_ordenados_por_punto_de_codigo()
    {
        var spy = new SpyTool();
        var (gate, log, _) = CreateGate(spy);

        var req = new ActionRequest(
            new ActionId("agendar"),
            new Dictionary<string, string>
            {
                ["z_arg"] = "ultimo",
                ["a_arg"] = "primero",
                ["m_arg"] = "medio",
            },
            "prof-1");

        gate.Submit(req);

        Assert.Single(spy.Calls);
        var argsRecibidos = spy.Calls[0].Arguments.Keys.ToList();
        Assert.Equal(["a_arg", "m_arg", "z_arg"], argsRecibidos);

        var eReq = log.First();
        var clavesEvento = eReq.Data!.Keys.Where(k => k != "by").ToList();
        Assert.Equal(["a_arg", "m_arg", "z_arg"], clavesEvento);
    }

    [Fact]
    public void Por_encima_del_techo_rechaza_con_needs_authorization_sin_llamar_a_la_herramienta()
    {
        var spy = new SpyTool();
        var catalog = ActionCatalog.Parse([
            KeyValuePair.Create("accion_irr", "irreversible"),
            KeyValuePair.Create("accion_rwc", "reversible_with_cost"),
        ], "test");

        var (gate, log, _) = CreateGate(spy, catalog, ceiling: Reversibility.Reversible);

        var req1 = new ActionRequest(new ActionId("accion_irr"), new Dictionary<string, string>(), "u1");
        var res1 = gate.Submit(req1);
        Assert.Equal(ToolStatus.Rejected, res1.Status);
        Assert.Equal("needs_authorization", res1.Reason);

        var req2 = new ActionRequest(new ActionId("accion_rwc"), new Dictionary<string, string>(), "u1");
        var res2 = gate.Submit(req2);
        Assert.Equal(ToolStatus.Rejected, res2.Status);
        Assert.Equal("needs_authorization", res2.Reason);

        Assert.Empty(spy.Calls);
        var events = log.ToList();
        Assert.Equal(4, events.Count);
        Assert.Equal(EventKind.ActionRequested, events[0].Kind);
        Assert.Equal(EventKind.ActionRejected, events[1].Kind);
        Assert.Equal(EventValue.Of("needs_authorization"), events[1].Data!["reason"]);
        Assert.Equal(EventKind.ActionRequested, events[2].Kind);
        Assert.Equal(EventKind.ActionRejected, events[3].Kind);
        Assert.Equal(EventValue.Of("needs_authorization"), events[3].Data!["reason"]);
    }

    [Fact]
    public void En_el_techo_se_ejecuta()
    {
        var spy = new SpyTool();
        var catalog = ActionCatalog.Parse([
            KeyValuePair.Create("accion_rev", "reversible"),
            KeyValuePair.Create("accion_rwc", "reversible_with_cost"),
        ], "test");

        // 1. Reversible con techo Reversible
        var (gate1, _, _) = CreateGate(spy, catalog, ceiling: Reversibility.Reversible);
        var res1 = gate1.Submit(new ActionRequest(new ActionId("accion_rev"), new Dictionary<string, string>(), "u1"));
        Assert.Equal(ToolStatus.Done, res1.Status);

        // 2. ReversibleWithCost con techo ReversibleWithCost
        var (gate2, _, _) = CreateGate(spy, catalog, ceiling: Reversibility.ReversibleWithCost);
        var res2 = gate2.Submit(new ActionRequest(new ActionId("accion_rwc"), new Dictionary<string, string>(), "u1"));
        Assert.Equal(ToolStatus.Done, res2.Status);

        Assert.Equal(2, spy.Calls.Count);
    }

    [Fact]
    public void Los_argumentos_sobrescriben_by_y_los_datos_sobrescriben_reason()
    {
        var spy = new SpyTool
        {
            Result = new ToolResult(
                ToolStatus.Done,
                "base_reason",
                new Dictionary<string, string> { ["reason"] = "custom_reason" }),
        };

        var (gate, log, _) = CreateGate(spy);

        var req = new ActionRequest(
            new ActionId("agendar"),
            new Dictionary<string, string> { ["by"] = "override_by" },
            "u1");

        gate.Submit(req);

        var events = log.ToList();
        Assert.Equal(2, events.Count);

        // Los argumentos sobrescriben 'by'
        Assert.Equal(EventValue.Of("override_by"), events[0].Data!["by"]);

        // Los datos del resultado sobrescriben 'reason'
        Assert.Equal(EventValue.Of("custom_reason"), events[1].Data!["reason"]);
    }

    [Fact]
    public void Dos_ticks_por_peticion()
    {
        var spy = new SpyTool();
        var (gate, log, _) = CreateGate(spy);

        var req1 = new ActionRequest(new ActionId("agendar"), new Dictionary<string, string>(), "u1");
        gate.Submit(req1);

        var req2 = new ActionRequest(new ActionId("cancelar"), new Dictionary<string, string>(), "u1");
        gate.Submit(req2);

        var events = log.ToList();
        Assert.Equal(4, events.Count);
        Assert.Equal(1L, events[0].Tick);
        Assert.Equal(2L, events[1].Tick);
        Assert.Equal(3L, events[2].Tick);
        Assert.Equal(4L, events[3].Tick);
    }
}
