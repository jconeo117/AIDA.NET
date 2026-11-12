using Aida.Engine.Ontology;
using Aida.Engine.Serialization;

namespace Aida.Engine.Packs.Checks;

/// <summary>Comprobaciones 7 y 8: las referencias de las metas, sus ciclos, y que toda meta pueda cumplirse (<c>pack.py:294-429</c>).</summary>
/// <remarks>
/// Un id repetido en las metas de un agente se trata como lo hace un <c>dict</c> de Python: la clave queda en la posición
/// de su primera aparición y el valor es el de la última. La comprobación 8 recorre esas entradas en ese orden, porque
/// suma decimales y el orden cambia el último bit.
/// </remarks>
internal static class GoalChecks
{
    /// <summary>Comprobación 7: ejes, rasgos y <c>requires_goal</c> existen, y no hay ciclos.</summary>
    /// <param name="pack">El contenido leído.</param>
    /// <returns>Los mensajes, en el orden de la 0.1.</returns>
    internal static List<string> GoalRefs(PackContents pack)
    {
        var needs = pack.Needs.Select(need => need.Id.Value).ToHashSet(StringComparer.Ordinal);
        var traits = pack.Traits.Select(trait => trait.Id.Value).ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var agent in pack.Agents)
        {
            var owned = OrderedLast(agent.Goals).ToDictionary(goal => goal.Id.Value, StringComparer.Ordinal);
            var agentName = PythonText.Repr(agent.Id.Value);
            foreach (var goal in agent.Goals)
            {
                var name = $"agente {agentName}, meta {PythonText.Repr(goal.Id.Value)}";
                if (!needs.Contains(goal.Need.Value))
                {
                    problems.Add($"{name}: necesidad desconocida {PythonText.Repr(goal.Need.Value)}");
                }

                problems.AddRange(GoalTraitRefs(name, goal, traits));
                if (goal.RequiresGoal is { } required)
                {
                    if (!owned.ContainsKey(required.Value))
                    {
                        problems.Add($"{name}: espera a {PythonText.Repr(required.Value)}, que no es una meta suya");
                    }
                    else
                    {
                        AddCycle(owned, goal.Id, $"agente {agentName}: ciclo de metas ", problems);
                    }
                }
            }
        }

        var byId = OrderedLast(pack.Goals).ToDictionary(goal => goal.Id.Value, StringComparer.Ordinal);
        foreach (var goal in pack.Goals)
        {
            var name = $"meta {PythonText.Repr(goal.Id.Value)}";
            if (!needs.Contains(goal.Need.Value))
            {
                problems.Add($"{name}: necesidad desconocida {PythonText.Repr(goal.Need.Value)}");
            }

            problems.AddRange(GoalTraitRefs(name, goal, traits));
            if (goal.RequiresGoal is { } required)
            {
                if (!byId.ContainsKey(required.Value))
                {
                    problems.Add($"{name}: espera a {PythonText.Repr(required.Value)}, que no existe");
                }
                else
                {
                    AddCycle(byId, goal.Id, "ciclo de metas ", problems);
                }
            }
        }

        return problems;
    }

    /// <summary>Comprobación 8: toda meta puede cumplirse con el catálogo declarado.</summary>
    /// <param name="pack">El contenido leído.</param>
    /// <returns>Los mensajes, en el orden de la 0.1.</returns>
    internal static List<string> GoalsAreReachable(PackContents pack)
    {
        var moved = pack.Actions
            .SelectMany(action => action.Effects.Where(entry => entry.Value > 0.0).Select(entry => entry.Key.Value))
            .ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var agent in pack.Agents)
        {
            var concede = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var other in OrderedLast(agent.Goals))
            {
                AddConcessions(concede, other);
            }

            foreach (var goal in agent.Goals)
            {
                var name = $"agente {PythonText.Repr(agent.Id.Value)}, meta {PythonText.Repr(goal.Id.Value)}";
                if (!moved.Contains(goal.Need.Value))
                {
                    problems.Add($"{name}: ninguna acción sube {PythonText.Repr(goal.Need.Value)}, así que nunca podrá cumplirse");
                }

                foreach (var pair in goal.AtTraits.OrderBy(entry => entry.Key.Value, CodePointComparer.Instance))
                {
                    var has = TraitValue(agent.Traits, pair.Key.Value);
                    var reachable = has + Conceded(concede, pair.Key.Value);
                    if (reachable < pair.Value)
                    {
                        problems.Add($"{name}: pide {PythonText.Repr(pair.Key.Value)} a {PythonText.Float(pair.Value)}, tiene {PythonText.Float(has)} y ninguna de sus metas se lo sube — solo las derivas mueven un rasgo");
                    }
                }
            }
        }

        var concedePack = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var other in pack.Goals)
        {
            AddConcessions(concedePack, other);
        }

        foreach (var goal in pack.Goals)
        {
            var name = $"meta {PythonText.Repr(goal.Id.Value)}";
            if (!moved.Contains(goal.Need.Value))
            {
                problems.Add($"{name}: ninguna acción sube {PythonText.Repr(goal.Need.Value)}, así que nunca podrá cumplirse");
            }

            foreach (var pair in goal.AtTraits.OrderBy(entry => entry.Key.Value, CodePointComparer.Instance))
            {
                if (Conceded(concedePack, pair.Key.Value) < pair.Value)
                {
                    problems.Add($"{name}: pide {PythonText.Repr(pair.Key.Value)} a {PythonText.Float(pair.Value)} y ninguna meta se lo sube");
                }
            }
        }

        return problems;
    }

    // Lo que una meta sube a los rasgos, sumado con max(delta, 0.0) de Python, en el orden de declaración.
    private static void AddConcessions(Dictionary<string, double> concede, GoalSpec goal)
    {
        foreach (var drifts in new[] { goal.OnSuccess, goal.OnFailure })
        {
            foreach (var pair in drifts)
            {
                concede[pair.Key.Value] = Conceded(concede, pair.Key.Value) + PythonMax(pair.Value);
            }
        }
    }

    private static double Conceded(Dictionary<string, double> concede, string trait) =>
        concede.TryGetValue(trait, out var value) ? value : 0.0;

    private static double TraitValue(DeclaredMap<TraitId, double> traits, string trait) =>
        traits.TryGetValue(new TraitId(trait), out var value) ? value : 0.0;

    // max(x, 0.0) de Python devuelve x salvo que 0.0 sea estrictamente mayor: un NaN se conserva.
    private static double PythonMax(double value) => 0.0 > value ? 0.0 : value;

    private static List<string> GoalTraitRefs(string name, GoalSpec goal, HashSet<string> traits)
    {
        var problems = new List<string>();
        problems.AddRange(ReferenceChecks.UnknownKeys($"{name}.at_traits", "rasgo", goal.AtTraits.Keys.Select(key => key.Value), traits));
        problems.AddRange(ReferenceChecks.UnknownKeys($"{name}.on_success", "rasgo", goal.OnSuccess.Keys.Select(key => key.Value), traits));
        problems.AddRange(ReferenceChecks.UnknownKeys($"{name}.on_failure", "rasgo", goal.OnFailure.Keys.Select(key => key.Value), traits));
        return problems;
    }

    // Sigue requires_goal mientras la meta exista; si vuelve a una ya vista, hay ciclo. Sin prefijo en la raíz del pack.
    private static void AddCycle(Dictionary<string, GoalSpec> goals, GoalId start, string prefix, List<string> problems)
    {
        var seen = new List<string>();
        string? actual = start.Value;
        while (actual is not null && goals.TryGetValue(actual, out var current))
        {
            if (seen.Contains(actual))
            {
                var chain = string.Join(" -> ", seen.Append(actual));
                problems.Add($"{prefix}{chain}. Ninguna se abriría jamás");
                return;
            }

            seen.Add(actual);
            actual = current.RequiresGoal?.Value;
        }
    }

    // dict {g.id: g for g in goals} de Python, recorrido en orden: cada id en la posición de su primera aparición,
    // con la meta de su última aparición. Un Dictionary de .NET no garantiza ese orden, así que se guarda la lista.
    private static List<GoalSpec> OrderedLast(IEnumerable<GoalSpec> goals)
    {
        var order = new List<string>();
        var last = new Dictionary<string, GoalSpec>(StringComparer.Ordinal);
        foreach (var goal in goals)
        {
            if (!last.ContainsKey(goal.Id.Value))
            {
                order.Add(goal.Id.Value);
            }

            last[goal.Id.Value] = goal;
        }

        return order.Select(id => last[id]).ToList();
    }
}
