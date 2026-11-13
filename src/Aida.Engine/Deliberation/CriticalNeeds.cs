using Aida.Engine.Numerics;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;
using Aida.Engine.State;

namespace Aida.Engine.Deliberation;

/// <summary>Las necesidades críticas: las que están tan bajas que deben interrumpir lo que el agente hace
/// (<c>decide.py:339-409</c>).</summary>
/// <remarks>
/// El umbral baja con el progreso de la acción en curso y con el coste de cambio (<c>switch_cost</c>): a mitad de camino,
/// soltar algo cuesta más. Se ignoran las necesidades con <c>urgent_when_low = false</c>, que se acumulan y no son una
/// emergencia. La división entre los ticks es exacta mientras las duraciones sean menores que 2^53.
/// </remarks>
internal static class CriticalNeeds
{
    /// <summary>Todas las necesidades críticas, la más urgente primero.</summary>
    /// <param name="execution">La corrida.</param>
    /// <param name="pack">El pack.</param>
    /// <returns>Las necesidades por debajo del umbral, ordenadas por urgencia descendente y, si empatan, por identificador.</returns>
    internal static List<NeedId> All(ExecutionState execution, ContentPack pack)
    {
        var agent = execution.Agent;
        double threshold = agent.Temperament.CriticalNeedThreshold;
        if (agent.Performing is { TotalTicks: > 0 } performing)
        {
            double progress = PyFloat.Max(0.0, PyFloat.Min(1.0, 1.0 - ((double)performing.TicksRemaining / performing.TotalTicks)));
            threshold *= 1.0 - (progress * agent.Temperament.SwitchCost);
        }

        var urgent = new List<NeedId>();
        foreach (var (needId, value) in agent.Needs)
        {
            if (!pack.NeedById.TryGetValue(needId, out var need) || !need.UrgentWhenLow)
            {
                continue;
            }

            if (value < threshold)
            {
                urgent.Add(needId);
            }
        }

        urgent.Sort((a, b) =>
        {
            double ua = agent.Urgency(pack.NeedById[a]);
            double ub = agent.Urgency(pack.NeedById[b]);
            if (ua > ub)
            {
                return -1;
            }

            return ua < ub ? 1 : ((IComparer<NeedId>)IdOrder.Instance).Compare(a, b);
        });
        return urgent;
    }

    /// <summary>La necesidad crítica más urgente, si merece interrumpir lo que el agente hace.</summary>
    /// <param name="execution">La corrida.</param>
    /// <param name="pack">El pack.</param>
    /// <returns>La necesidad, o <see langword="null"/> si no hay ninguna crítica, o si la acción en curso ya atiende la
    /// más urgente (no tiene sentido interrumpirla por lo que ya cubre).</returns>
    internal static NeedId? Worst(ExecutionState execution, ContentPack pack)
    {
        var urgent = All(execution, pack);
        if (urgent.Count == 0)
        {
            return null;
        }

        var worst = urgent[0];
        var performing = execution.Agent.Performing;
        if (performing is not null
            && (pack.ActionById[performing.Action].Satisfies.TryGetValue(worst, out var s) ? s : 0.0) > 0.0)
        {
            return null;
        }

        return worst;
    }
}
