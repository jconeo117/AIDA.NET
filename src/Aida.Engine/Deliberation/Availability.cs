using Aida.Engine.Ontology;
using Aida.Engine.State;

namespace Aida.Engine.Deliberation;

/// <summary>La puerta de disponibilidad: si el agente puede hacer una acción ahora mismo (<c>decide.py:174-189</c>).</summary>
/// <remarks>
/// Devuelve el nombre literal de la puerta que falla, en vez de un booleano, porque la explicación y el dorado lo
/// comparan como texto. Se recorre por identificador: la primera puerta que falla es la que se informa.
/// </remarks>
internal static class Availability
{
    /// <summary>La primera puerta que impide hacer la acción, o <see langword="null"/> si el agente puede hacerla.</summary>
    /// <param name="agent">El agente.</param>
    /// <param name="action">La acción a comprobar.</param>
    /// <returns><c>"requires"</c> si falta un nivel mínimo de necesidad; <c>"requires_traits"</c> si falta un rasgo; o
    /// <see langword="null"/> si está disponible.</returns>
    internal static string? Check(AgentState agent, ActionSpec action)
    {
        foreach (var needId in action.Requires.Keys.OrderBy(n => n, IdOrder.Instance))
        {
            double value = agent.Needs.TryGetValue(needId, out var v) ? v : 0.0;
            if (value < action.Requires[needId])
            {
                return "requires";
            }
        }

        foreach (var traitId in action.RequiresTraits.Keys.OrderBy(t => t, IdOrder.Instance))
        {
            double value = agent.Traits.TryGetValue(traitId, out var v) ? v : 0.0;
            if (value < action.RequiresTraits[traitId])
            {
                return "requires_traits";
            }
        }

        return null;
    }
}
