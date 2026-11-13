using Aida.Engine.Ontology;
using Aida.Engine.Packs;
using Aida.Engine.State;

namespace Aida.Engine.Deliberation;

/// <summary>El beneficio, el coste y la novedad de una acción para un agente (<c>decide.py:78-145</c>).</summary>
/// <remarks>
/// El total empieza en <c>0.0</c> y suma, en vez de arrancar en el primer término: <c>0.0 + (-0.0)</c> es <c>0.0</c>, y
/// así el signo de un cero no cambia el resultado. El factor de creencias (1.0) se omite porque multiplicar por 1.0 es
/// exacto; las creencias llegan con la unidad de aprendizaje. El apremio sale de <see cref="AgentState.Urgency"/>, la
/// única puerta de urgencia.
/// </remarks>
internal static class Utility
{
    /// <summary>El beneficio de una acción: lo que alivia, ponderado por peso y urgencia.</summary>
    /// <param name="pack">El pack.</param>
    /// <param name="agent">El agente.</param>
    /// <param name="action">La acción.</param>
    /// <returns>El beneficio, sobre <see cref="ActionSpec.Satisfies"/>.</returns>
    internal static double Benefit(ContentPack pack, AgentState agent, ActionSpec action) =>
        Weigh(pack, agent, action.Satisfies);

    /// <summary>El coste de una acción: lo que gasta, ponderado por peso y urgencia.</summary>
    /// <param name="pack">El pack.</param>
    /// <param name="agent">El agente.</param>
    /// <param name="action">La acción.</param>
    /// <returns>El coste, sobre <see cref="ActionSpec.Costs"/>.</returns>
    internal static double Cost(ContentPack pack, AgentState agent, ActionSpec action) =>
        Weigh(pack, agent, action.Costs);

    /// <summary>El factor de novedad: lo que se ha cansado el agente de la acción, según el peso de la novedad.</summary>
    /// <param name="agent">El agente.</param>
    /// <param name="action">La acción.</param>
    /// <param name="weight">El peso de la novedad del temperamento.</param>
    /// <returns><c>1 - saturación × peso</c>. Vale 1.0 si el agente nunca ha hecho la acción.</returns>
    internal static double Novelty(AgentState agent, ActionId action, double weight) =>
        1.0 - ((agent.Saturation.TryGetValue(action, out var saturation) ? saturation : 0.0) * weight);

    // Suma, en orden de identificador, peso × urgencia × magnitud para cada necesidad del mapa.
    private static double Weigh(ContentPack pack, AgentState agent, DeclaredMap<NeedId, double> terms)
    {
        double total = 0.0;
        foreach (var needId in terms.Keys.OrderBy(n => n, IdOrder.Instance))
        {
            var need = pack.NeedById[needId];
            double pressure = agent.Urgency(need);
            total += agent.NeedWeights[needId] * pressure * terms[needId];
        }

        return total;
    }
}
