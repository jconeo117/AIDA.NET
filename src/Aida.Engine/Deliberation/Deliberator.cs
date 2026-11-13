using Aida.Engine.Numerics;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;
using Aida.Engine.Randomness;
using Aida.Engine.State;

namespace Aida.Engine.Deliberation;

/// <summary>Elige qué hacer: puntúa las acciones disponibles, desempata por identificador y sortea entre las mejores
/// (<c>decide.py:66-75, 192-336</c>).</summary>
/// <remarks>
/// <para>La utilidad es <c>(beneficio × novedad) − aversión × coste</c>, y después se multiplica por el gusto hacia la
/// acción. El desempate es el de <c>_rank</c>: utilidad descendente y, si empata, el identificador por punto de código.
/// Los pesos del sorteo son <c>exp((u − mejor) / T)</c>, con el sorteo derivado de la clave (agente, tick): dar lo
/// mismo en cualquier orden de llamadas.</para>
/// <para>Una utilidad NaN no tiene orden definido en la 0.1. El pack no la produce, así que queda fuera del alcance.</para>
/// </remarks>
internal static class Deliberator
{
    /// <summary>Puntúa las acciones del catálogo que el agente puede hacer ahora.</summary>
    /// <param name="execution">La corrida: agente, tick y fuente de aleatoriedad.</param>
    /// <param name="pack">El pack.</param>
    /// <returns>Los candidatos, y para cada acción descartada, la puerta que la impide.</returns>
    internal static (List<Decision> Pool, IReadOnlyDictionary<ActionId, string> Discarded) Candidates(
        ExecutionState execution, ContentPack pack)
    {
        var agent = execution.Agent;
        var pool = new List<Decision>();
        var discarded = new SortedDictionary<ActionId, string>(IdOrder.Instance);
        foreach (var actionId in pack.ActionById.Keys.OrderBy(a => a, IdOrder.Instance))
        {
            var action = pack.ActionById[actionId];
            var gate = Availability.Check(agent, action);
            if (gate is not null)
            {
                discarded[action.Id] = gate;
                continue;
            }

            double b = Utility.Benefit(pack, agent, action);
            double c = Utility.Cost(pack, agent, action);
            double nov = Utility.Novelty(agent, action.Id, agent.Temperament.NoveltyWeight);
            double util = (b * nov) - (agent.Temperament.LossAversion * c);
            util *= agent.Tastes.TryGetValue(action.Id.Value, out var taste) ? taste : 1.0;
            pool.Add(new Decision(action.Id, util, b, c, nov));
        }

        return (pool, discarded);
    }

    /// <summary>Los candidatos, filtrados por lo que el bucle exige: acciones prohibidas fuera y, si hay una necesidad
    /// que atender, solo las que la satisfacen.</summary>
    /// <param name="execution">La corrida.</param>
    /// <param name="pack">El pack.</param>
    /// <param name="mustAddress">La necesidad que hay que atender, o <see langword="null"/>.</param>
    /// <param name="forbidden">Las acciones que no pueden elegirse.</param>
    /// <returns>Los candidatos que quedan, y las acciones descartadas por disponibilidad.</returns>
    internal static (List<Decision> Pool, IReadOnlyDictionary<ActionId, string> Discarded) Deliberate(
        ExecutionState execution, ContentPack pack, NeedId? mustAddress, IReadOnlySet<ActionId> forbidden)
    {
        var (pool, discarded) = Candidates(execution, pack);
        if (forbidden.Count > 0)
        {
            pool = pool.Where(d => !forbidden.Contains(d.Action)).ToList();
        }

        if (mustAddress is { } need)
        {
            pool = pool
                .Where(d => (pack.ActionById[d.Action].Satisfies.TryGetValue(need, out var s) ? s : 0.0) > 0.0)
                .ToList();
        }

        return (pool, discarded);
    }

    /// <summary>La decisión del tick: la acción elegida, o <see langword="null"/> si ninguna puede hacerse.</summary>
    /// <param name="execution">La corrida.</param>
    /// <param name="pack">El pack.</param>
    /// <param name="mustAddress">La necesidad que hay que atender, o <see langword="null"/>. Si ninguna acción la atiende,
    /// no hay vuelta a la elección libre (<c>decide.py:275-280</c>).</param>
    /// <param name="forbidden">Las acciones que no pueden elegirse.</param>
    /// <returns>La decisión, o <see langword="null"/>.</returns>
    /// <exception cref="DivideByZeroException">Si la temperatura es cero: Python lanzaría <c>ZeroDivisionError</c>.</exception>
    internal static Decision? Decide(ExecutionState execution, ContentPack pack, NeedId? mustAddress, IReadOnlySet<ActionId> forbidden)
    {
        var (pool, _) = Deliberate(execution, pack, mustAddress, forbidden);
        if (pool.Count == 0)
        {
            return null;
        }

        pool.Sort(Rank);
        int take = (int)Math.Min(pack.Tuning.CandidatePool, pool.Count);
        var finalists = pool.GetRange(0, take);
        if (finalists.Count == 1)
        {
            return finalists[0];
        }

        double best = finalists[0].Utility;
        double temperature = execution.Agent.Temperament.Temperature;
        if (temperature == 0.0)
        {
            throw new DivideByZeroException("float division by zero");
        }

        var weights = new double[finalists.Count];
        for (int i = 0; i < finalists.Count; i++)
        {
            weights[i] = PyFloat.Exp((finalists[i].Utility - best) / temperature);
        }

        var rng = execution.Rng.ForDecision(execution.Agent.AgentId, execution.Tick);
        return finalists[WeightedChoice.Pick(rng, weights)];
    }

    // _rank de la 0.1 (decide.py:66-75): utilidad descendente y, si empata, el id por punto de código.
    private static int Rank(Decision a, Decision b)
    {
        if (a.Utility > b.Utility)
        {
            return -1;
        }

        if (a.Utility < b.Utility)
        {
            return 1;
        }

        return ((IComparer<ActionId>)IdOrder.Instance).Compare(a.Action, b.Action);
    }
}
