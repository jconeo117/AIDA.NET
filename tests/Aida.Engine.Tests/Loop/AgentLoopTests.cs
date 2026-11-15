using Aida.Engine.Events;
using Aida.Engine.Loop;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;
using Aida.Engine.Ports;

namespace Aida.Engine.Tests.Loop;

public sealed class AgentLoopTests
{
    private const string Yaml = """
        id: p_loop
        label: Loop Pack
        authority:
          autonomous_up_to: reversible
          confirmation_ttl_ticks: 5
        needs:
          - id: n1
            label: N1
            decay_per_tick: 0.1
        actions:
          - id: act_rev
            label: Rev
            reversibility: reversible
            duration_ticks: 1
            satisfies:
              n1: 0.2
            effects:
              n1: 0.2
          - id: act_cost
            label: Cost
            reversibility: reversible_with_cost
            duration_ticks: 1
            satisfies:
              n1: 0.9
            effects:
              n1: 0.9
        intents:
          - id: pedir_n1
            label: Pedir N1
            need: n1
            examples:
              - pedir ayuda
        agents:
          - id: ag1
            label: Agent 1
            initial_needs:
              n1: 0.5
        """;

    private readonly ContentPack _pack = PackLoader.Parse(Yaml);

    [Fact]
    public void Arranca_en_el_tick_cero_y_cada_paso_suma_uno()
    {
        var spec = _pack.Agents[0];
        var loop = AgentLoop.Start(_pack, spec, seed: 42, inboxCapacity: 10);

        Assert.Equal(0, loop.Execution.Tick);

        loop.Step();
        Assert.Equal(1, loop.Execution.Tick);

        loop.Step();
        Assert.Equal(2, loop.Execution.Tick);
    }

    [Fact]
    public void Sin_clasificador_solo_se_observa_la_entrada()
    {
        var spec = _pack.Agents[0];
        var loop = AgentLoop.Start(_pack, spec, seed: 42, inboxCapacity: 10, intent: null);

        loop.Inbox.Push(1, spec.Id, "hola sin clasificador");
        loop.Step();

        Assert.Contains(loop.Log, e => e.Kind == EventKind.InputObserved && e.Content == "hola sin clasificador");
        Assert.DoesNotContain(loop.Log, e => e.Kind == EventKind.IntentClassified);
    }

    [Fact]
    public void Un_id_que_no_esta_en_el_pack_se_registra_sin_intencion()
    {
        var spec = _pack.Agents[0];
        var classifier = new FixedClassifier("intencion_que_no_existe");
        var loop = AgentLoop.Start(_pack, spec, seed: 42, inboxCapacity: 10, intent: classifier);

        loop.Inbox.Push(1, spec.Id, "texto desconocido");
        loop.Step();

        var classified = loop.Log.FirstOrDefault(e => e.Kind == EventKind.IntentClassified);
        Assert.NotNull(classified);
        Assert.Empty(classified.Data);
    }

    [Fact]
    public void Aprobar_sin_propuesta_no_emite_nada()
    {
        var spec = _pack.Agents[0];
        var loop = AgentLoop.Start(_pack, spec, seed: 42, inboxCapacity: 10);

        var actionId = new ActionId("act_cost");
        Assert.False(loop.Approve(actionId));
        Assert.False(loop.Deny(actionId));

        Assert.DoesNotContain(loop.Log, e => e.Kind is EventKind.ActionAuthorized or EventKind.ActionDenied);
    }

    [Fact]
    public void Aprobar_y_rechazar_emiten_en_el_tick_actual()
    {
        var spec = _pack.Agents[0];
        var loop = AgentLoop.Start(_pack, spec, seed: 42, inboxCapacity: 10);

        // En el paso 1, act_cost supera el techo y se propone
        loop.Step();
        Assert.Equal(1, loop.Execution.Tick);
        var costId = new ActionId("act_cost");
        Assert.True(loop.Authority.Pending.ContainsKey(costId));

        // Aprobar en el tick 1
        Assert.True(loop.Approve(costId));
        var authEvent = loop.Log.Last(e => e.Kind == EventKind.ActionAuthorized);
        Assert.Equal(1, authEvent.Tick);
        Assert.Equal(costId, authEvent.Action);

        // Forzar otra propuesta para probar Deny
        loop.Authority.Propose(_pack.ActionById[costId], loop.Execution.Tick);
        Assert.True(loop.Deny(costId));
        var denyEvent = loop.Log.Last(e => e.Kind == EventKind.ActionDenied);
        Assert.Equal(1, denyEvent.Tick);
        Assert.Equal(costId, denyEvent.Action);
    }

    [Fact]
    public void Lo_que_supera_el_techo_se_propone_y_se_hace_otra_cosa()
    {
        var spec = _pack.Agents[0];
        var loop = AgentLoop.Start(_pack, spec, seed: 42, inboxCapacity: 10);

        // En tick 1:
        // act_cost (reversibility: reversible_with_cost) da mayor utilidad pero supera el techo (reversible).
        // Se propone act_cost, se veta, y se ejecuta act_rev.
        loop.Step();

        var costId = new ActionId("act_cost");
        var revId = new ActionId("act_rev");

        Assert.Contains(loop.Log, e => e.Kind == EventKind.ActionProposed && e.Action == costId);
        Assert.Contains(loop.Log, e => e.Kind == EventKind.ActionStarted && e.Action == revId);
    }

    private sealed class FixedClassifier(string? returnedIntent) : IIntentClassifier
    {
        public string? Classify(string utterance) => returnedIntent;
    }
}
