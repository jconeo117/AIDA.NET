using Aida.Engine.Events;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;
using Aida.Engine.State;

namespace Aida.Engine.Tests.State;

public sealed class AgentStateTests
{
    [Fact]
    public void Restore_con_cantidad_negativa_lanza_y_mensaje_empieza_segun_contrato()
    {
        const string Yaml = """
            id: p1
            label: P1
            needs:
              - id: n1
                label: N1
                decay_per_tick: 0.1
                max_debt: 0.5
            actions:
              - id: a1
                label: A1
                reversibility: reversible
                duration_ticks: 1
                satisfies:
                  n1: 0.5
                effects:
                  n1: 0.5
            agents:
              - id: ag1
                label: Ag1
            """;
        var pack = PackLoader.Parse(Yaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);
        var need = pack.Needs[0];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => agent.Restore(need.Id, -0.1, need));
        Assert.StartsWith("amount = -0.1, restore no admite cantidades negativas", ex.Message);
    }

    [Fact]
    public void Restore_paga_deuda_antes_de_subir_el_eje()
    {
        const string Yaml = """
            id: p1
            label: P1
            needs:
              - id: n1
                label: N1
                initial: 0.0
                decay_per_tick: 0.1
                max_debt: 0.5
            actions:
              - id: a1
                label: A1
                reversibility: reversible
                duration_ticks: 1
                satisfies:
                  n1: 0.5
                effects:
                  n1: 0.5
            agents:
              - id: ag1
                label: Ag1
            """;
        var pack = PackLoader.Parse(Yaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);
        var need = pack.Needs[0];

        // Simulamos un gasto que genera deuda
        agent.Apply(new DeclaredMap<NeedId, double>([new KeyValuePair<NeedId, double>(need.Id, -0.3)]), pack);
        Assert.Equal(0.0, agent.Needs[need.Id]);
        Assert.Equal(0.3, agent.Debt[need.Id]);

        // Reponemos 0.5: 0.3 debe pagar la deuda y 0.2 subir el eje
        var delivered = agent.Restore(need.Id, 0.5, need);
        Assert.Equal(0.2, delivered);
        Assert.Equal(0.2, agent.Needs[need.Id]);
        Assert.False(agent.Debt.ContainsKey(need.Id));
    }

    [Fact]
    public void Escalada_se_emite_exactamente_en_el_tick_del_umbral_y_no_en_los_siguientes()
    {
        const string Yaml = """
            id: p1
            label: P1
            needs:
              - id: n1
                label: N1
                initial: 0.0
                decay_per_tick: 0.1
                escalates_after: 3
            actions:
              - id: a1
                label: A1
                reversibility: reversible
                duration_ticks: 1
                satisfies:
                  n1: 0.5
                effects:
                  n1: 0.5
            agents:
              - id: ag1
                label: Ag1
            """;
        var pack = PackLoader.Parse(Yaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);
        var need = pack.Needs[0];
        var log = new EventLog();

        // Tick 1: racha 1, sin eventos
        agent.AdvanceFloorStreak(pack, log, 1);
        Assert.Empty(log);
        Assert.Equal(1, agent.FloorStreak[need.Id]);

        // Tick 2: racha 2, sin eventos
        agent.AdvanceFloorStreak(pack, log, 2);
        Assert.Empty(log);
        Assert.Equal(2, agent.FloorStreak[need.Id]);

        // Tick 3: racha 3 == escalates_after, se emite exactamente 1 evento
        agent.AdvanceFloorStreak(pack, log, 3);
        var ev = Assert.Single(log);
        Assert.Equal(3, agent.FloorStreak[need.Id]);
        Assert.Equal(EventKind.NeedEscalated, ev.Kind);
        Assert.Equal(3, ev.Tick);
        Assert.Equal(new AgentId("ag1"), ev.Owner);

        // Tick 4: racha 4 > escalates_after, no se emite ningún evento adicional
        agent.AdvanceFloorStreak(pack, log, 4);
        Assert.Single(log);
        Assert.Equal(4, agent.FloorStreak[need.Id]);
    }

    [Fact]
    public void Ciclo_de_vida_de_lo_que_se_hace_valida_rangos_y_limites()
    {
        var actionId = new ActionId("act1");

        // ticks_remaining negativo lanza
        var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => new PerformingAction(actionId, -1, 0));
        Assert.StartsWith("ticks_remaining = -1, no puede ser negativo", ex1.Message);

        // total_ticks negativo lanza
        var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => new PerformingAction(actionId, 0, -1));
        Assert.StartsWith("total_ticks = -1, no puede ser negativo", ex2.Message);

        // Advance se detiene en 0
        const string Yaml = """
            id: p1
            label: P1
            tuning:
              saturation_recovery: 0.1
            needs:
              - id: n1
                label: N1
                decay_per_tick: 0.1
            actions:
              - id: act1
                label: Act 1
                reversibility: reversible
                duration_ticks: 1
                satisfies:
                  n1: 0.5
                effects:
                  n1: 0.5
            agents:
              - id: ag1
                label: Ag1
            """;
        var pack = PackLoader.Parse(Yaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);

        agent.Performing = new PerformingAction(actionId, 1, 2);
        agent.Advance();
        Assert.NotNull(agent.Performing);
        Assert.Equal(0, agent.Performing.TicksRemaining);
        Assert.Equal(2, agent.Performing.TotalTicks);

        // Volver a avanzar no decrementa a negativo ni borra Performing
        agent.Advance();
        Assert.NotNull(agent.Performing);
        Assert.Equal(0, agent.Performing.TicksRemaining);

        // RecoverNovelty borra lo que llega a 0
        agent.Saturate(actionId, 0.2);
        Assert.Equal(0.2, agent.Saturation[actionId]);

        agent.RecoverNovelty(0.1);
        Assert.Equal(0.1, agent.Saturation[actionId]);

        agent.RecoverNovelty(0.1);
        Assert.False(agent.Saturation.ContainsKey(actionId));
    }

    [Fact]
    public void Meta_propia_que_no_esta_en_el_pack_no_cambia_los_pesos()
    {
        const string Yaml = """
            id: p1
            label: P1
            needs:
              - id: n1
                label: N1
                decay_per_tick: 0.1
                base_weight: 1.0
              - id: n2
                label: N2
                decay_per_tick: 0.1
                base_weight: 1.0
            goals:
              - id: g_pack
                label: Meta Pack
                need: n1
                at_least: 0.8
                weight_multiplier: 2.0
            actions:
              - id: a1
                label: A1
                reversibility: reversible
                duration_ticks: 1
                satisfies:
                  n1: 0.5
                  n2: 0.5
                effects:
                  n1: 0.5
                  n2: 0.5
            agents:
              - id: ag1
                label: Ag1
                goals:
                  - id: g_pack
                    label: Meta Propia 1
                    need: n1
                    at_least: 0.8
                    weight_multiplier: 5.0
                  - id: g_ajena
                    label: Meta Ajena
                    need: n2
                    at_least: 0.8
                    weight_multiplier: 10.0
            """;
        var pack = PackLoader.Parse(Yaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);

        // n1 debe usar el weightMultiplier del PACK (2.0), no el del agente (5.0)
        Assert.Equal(2.0, agent.NeedWeights[new NeedId("n1")]);

        // n2 no tiene meta reconocida en el pack, así que su peso sigue siendo el baseWeight (1.0)
        Assert.Equal(1.0, agent.NeedWeights[new NeedId("n2")]);
    }
}
