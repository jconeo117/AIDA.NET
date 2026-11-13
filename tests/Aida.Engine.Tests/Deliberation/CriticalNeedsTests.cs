using Aida.Engine.Deliberation;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;
using Aida.Engine.Randomness;
using Aida.Engine.State;

namespace Aida.Engine.Tests.Deliberation;

public sealed class CriticalNeedsTests
{
    private const string Yaml = """
        id: p_critical
        label: Critical Needs Pack
        tuning:
          critical_need_threshold: 0.5
          switch_cost: 0.5
        needs:
          - id: n1
            label: Need 1
            decay_per_tick: 0.1
            urgent_when_low: true
          - id: n2
            label: Need 2
            decay_per_tick: 0.1
            urgent_when_low: false
        actions:
          - id: a1
            label: Action 1
            reversibility: reversible
            duration_ticks: 10
            satisfies:
              n1: 0.5
            effects:
              n1: 0.5
          - id: a2
            label: Action 2
            reversibility: reversible
            duration_ticks: 10
            satisfies:
              n2: 0.5
            effects:
              n2: 0.5
        agents:
          - id: ag1
            label: Agent 1
            initial_needs:
              n1: 0.4
              n2: 0.1
        """;

    [Fact]
    public void El_umbral_baja_con_el_progreso_de_la_accion()
    {
        var pack = PackLoader.Parse(Yaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);
        var exec = new ExecutionState(agent, 1, new RngSource(42));

        // Sin acción en curso: n1 (0.4) < threshold (0.5) -> crítica
        var initialUrgent = CriticalNeeds.All(exec, pack);
        Assert.Contains(new NeedId("n1"), initialUrgent);

        // Con acción avanzada al 100%: progress = 1.0, threshold = 0.5 * (1 - 1.0 * 0.5) = 0.25
        // n1 (0.4) >= 0.25 -> ya no es crítica
        agent.Performing = new PerformingAction(new ActionId("a2"), ticksRemaining: 0, totalTicks: 10);
        var progressedUrgent = CriticalNeeds.All(exec, pack);
        Assert.DoesNotContain(new NeedId("n1"), progressedUrgent);
    }

    [Fact]
    public void Lo_que_no_urge_al_bajar_no_cuenta()
    {
        var pack = PackLoader.Parse(Yaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);
        var exec = new ExecutionState(agent, 1, new RngSource(42));

        // n2 tiene urgent_when_low = false y valor 0.1 (< 0.5)
        var urgent = CriticalNeeds.All(exec, pack);
        Assert.DoesNotContain(new NeedId("n2"), urgent);
    }

    [Fact]
    public void La_accion_en_curso_que_atiende_la_peor_no_se_interrumpe()
    {
        var pack = PackLoader.Parse(Yaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);
        var exec = new ExecutionState(agent, 1, new RngSource(42));

        // n1 es la peor necesidad crítica
        // Si performing ejecuta a1 (que atiende n1), Worst devuelve null para no interrumpir
        agent.Performing = new PerformingAction(new ActionId("a1"), ticksRemaining: 10, totalTicks: 10);
        Assert.Null(CriticalNeeds.Worst(exec, pack));

        // Si performing ejecuta a2 (que NO atiende n1), Worst devuelve n1
        agent.Performing = new PerformingAction(new ActionId("a2"), ticksRemaining: 10, totalTicks: 10);
        Assert.Equal(new NeedId("n1"), CriticalNeeds.Worst(exec, pack));
    }
}
