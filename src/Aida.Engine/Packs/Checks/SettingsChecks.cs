using Aida.Engine.Serialization;

namespace Aida.Engine.Packs.Checks;

/// <summary>Comprobaciones 14 y 15: la salud apunta a acciones existentes, y las cotas iniciales y de metas están en rango (<c>pack.py:553-585</c>).</summary>
internal static class SettingsChecks
{
    /// <summary>Comprobación 14: <c>health.may_go_unused</c> apunta a acciones que existen.</summary>
    /// <param name="pack">El contenido leído.</param>
    /// <returns>Los mensajes, en orden de identificador.</returns>
    internal static List<string> HealthRefs(PackContents pack)
    {
        var known = pack.Actions.Select(action => action.Id.Value).ToHashSet(StringComparer.Ordinal);
        return pack.Health.MayGoUnused
            .Select(action => action.Value)
            .OrderBy(id => id, CodePointComparer.Instance)
            .Where(id => !known.Contains(id))
            .Select(id => "health.may_go_unused: acción desconocida " + PythonText.Repr(id))
            .ToList();
    }

    /// <summary>Comprobación 15: las cotas iniciales en [0, 1] y las de las metas en [-2, 2].</summary>
    /// <param name="pack">El contenido leído.</param>
    /// <returns>Los mensajes, en el orden de agentes, metas y metas del pack.</returns>
    internal static List<string> DeclaredBounds(PackContents pack)
    {
        var problems = new List<string>();
        foreach (var agent in pack.Agents)
        {
            foreach (var pair in agent.InitialNeeds.OrderBy(entry => entry.Key.Value, CodePointComparer.Instance))
            {
                if (!(pair.Value >= 0.0 && pair.Value <= 1.0))
                {
                    problems.Add("agente " + PythonText.Repr(agent.Id.Value) + ".initial_needs[" + PythonText.Repr(pair.Key.Value) + "] = "
                        + PythonText.Float(pair.Value) + ", fuera de [0.0, 1.0]");
                }
            }

            foreach (var goal in agent.Goals)
            {
                foreach (var pair in goal.AtTraits.OrderBy(entry => entry.Key.Value, CodePointComparer.Instance))
                {
                    if (!(pair.Value >= -2.0 && pair.Value <= 2.0))
                    {
                        problems.Add("agente " + PythonText.Repr(agent.Id.Value) + ", meta " + PythonText.Repr(goal.Id.Value) + ".at_traits["
                            + PythonText.Repr(pair.Key.Value) + "] = " + PythonText.Float(pair.Value) + ", fuera de [-2.0, 2.0]");
                    }
                }
            }
        }

        foreach (var goal in pack.Goals)
        {
            foreach (var pair in goal.AtTraits.OrderBy(entry => entry.Key.Value, CodePointComparer.Instance))
            {
                if (!(pair.Value >= -2.0 && pair.Value <= 2.0))
                {
                    problems.Add("meta " + PythonText.Repr(goal.Id.Value) + ".at_traits[" + PythonText.Repr(pair.Key.Value) + "] = "
                        + PythonText.Float(pair.Value) + ", fuera de [-2.0, 2.0]");
                }
            }
        }

        return problems;
    }
}
