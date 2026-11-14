using Aida.Engine.Deliberation;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;
using Aida.Engine.Randomness;
using Aida.Engine.State;

namespace Aida.Engine.Tests.Deliberation;

public sealed class DeliberatorTests
{
    private const string Yaml = """
        id: p_delib
        label: Deliberation Pack
        tuning:
          candidate_pool: 1
        needs:
          - id: n1
            label: N1
            decay_per_tick: 0.1
          - id: n2
            label: N2
            decay_per_tick: 0.1
        traits:
          - id: t1
            label: T1
        actions:
          - id: Z
            label: Action Z
            reversibility: reversible
            duration_ticks: 1
            satisfies:
              n1: 0.5
            effects:
              n1: 0.5
          - id: a
            label: Action a
            reversibility: reversible
            duration_ticks: 1
            satisfies:
              n1: 0.5
            effects:
              n1: 0.5
          - id: req_both
            label: Action Requiring Both
            reversibility: reversible
            duration_ticks: 1
            requires:
              n1: 0.9
            requires_traits:
              t1: 0.9
            satisfies:
              n1: 0.1
            effects:
              n1: 0.1
        agents:
          - id: ag1
            label: Agent 1
            initial_needs:
              n1: 0.5
          - id: ag_reach
            label: Agent Reachable
            traits:
              t1: 1.0
            initial_needs:
              n1: 1.0
        """;

    [Fact]
    public void Sin_candidatas_devuelve_null()
    {
        var pack = PackLoader.Parse(Yaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);
        var exec = new ExecutionState(agent, 1, new RngSource(42));

        var forbidden = new HashSet<ActionId> { new("Z"), new("a") };
        var decision = Deliberator.Decide(exec, pack, null, forbidden);

        Assert.Null(decision);
    }

    [Fact]
    public void Must_address_sin_acciones_que_lo_atiendan_devuelve_null()
    {
        var pack = PackLoader.Parse(Yaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);
        var exec = new ExecutionState(agent, 1, new RngSource(42));

        var decision = Deliberator.Decide(exec, pack, new NeedId("n2"), new HashSet<ActionId>());

        Assert.Null(decision);
    }

    [Fact]
    public void Un_solo_finalista_no_sortea()
    {
        const string SingleActionYaml = """
            id: p_single
            label: Single Action Pack
            tuning:
              candidate_pool: 5
            needs:
              - id: n1
                label: N1
                decay_per_tick: 0.1
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
        var pack = PackLoader.Parse(SingleActionYaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);
        var exec = new ExecutionState(agent, 1, new RngSource(42));

        // Con 1 solo finalista lo retorna de inmediato sin sortear
        var decision = Deliberator.Decide(exec, pack, null, new HashSet<ActionId>());
        Assert.NotNull(decision);
        Assert.Equal(new ActionId("a1"), decision.Action);
    }

    [Fact]
    public void El_desempate_es_por_id_en_punto_de_codigo()
    {
        var pack = PackLoader.Parse(Yaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);
        var exec = new ExecutionState(agent, 1, new RngSource(42));

        var decision = Deliberator.Decide(exec, pack, null, new HashSet<ActionId>());

        Assert.NotNull(decision);
        Assert.Equal(new ActionId("Z"), decision.Action);

        var rankMethod = typeof(Deliberator).GetMethod("Rank", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var decZ = new Decision(new ActionId("Z"), 1.0, 1.0, 0.0, 1.0);
        var decA = new Decision(new ActionId("a"), 1.0, 1.0, 0.0, 1.0);
        var rankResult = (int)rankMethod.Invoke(null, [decZ, decA])!;
        Assert.True(rankResult < 0, "Z debe clasificar antes que a en un empate exacto de utilidad");
    }

    [Fact]
    public void La_puerta_requires_va_antes_que_requires_traits()
    {
        var pack = PackLoader.Parse(Yaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);
        var action = pack.ActionById[new ActionId("req_both")];

        var gate = Availability.Check(agent, action);

        Assert.Equal("requires", gate);
    }

    [Fact]
    public void Lo_vetado_no_se_elige()
    {
        var pack = PackLoader.Parse(Yaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);
        var exec = new ExecutionState(agent, 1, new RngSource(42));

        var forbidden = new HashSet<ActionId> { new("Z") };
        var decision = Deliberator.Decide(exec, pack, null, forbidden);

        Assert.NotNull(decision);
        Assert.Equal(new ActionId("a"), decision.Action);
    }
}
