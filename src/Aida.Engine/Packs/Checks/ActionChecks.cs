using Aida.Engine.Ontology;
using Aida.Engine.Serialization;

namespace Aida.Engine.Packs.Checks;

/// <summary>Comprobaciones 9, 11, 12 y 13: intenciones atendibles, costes declarados, precondiciones y acciones alcanzables (<c>pack.py:431-551</c>).</summary>
internal static class ActionChecks
{
    /// <summary>Comprobación 9: toda intención reconocida tiene alguna acción que suba su necesidad (por <c>satisfies</c>).</summary>
    /// <param name="pack">El contenido leído.</param>
    /// <returns>Los mensajes, en el orden de las intenciones.</returns>
    internal static List<string> IntentsAreActionable(PackContents pack)
    {
        var moved = MovedBy(pack, action => action.Satisfies);
        return pack.Intents
            .Where(intent => !moved.Contains(intent.Need.Value))
            .Select(intent => $"intención {PythonText.Repr(intent.Id.Value)}: ninguna acción sube {PythonText.Repr(intent.Need.Value)}, así que reconocerla nunca llevaría a nada")
            .ToList();
    }

    /// <summary>Comprobación 11: todo efecto negativo tiene su coste declarado, o un 0.0 explícito.</summary>
    /// <param name="pack">El contenido leído.</param>
    /// <returns>Los mensajes, en el orden de las acciones y de sus necesidades.</returns>
    internal static List<string> CostsAreDeclared(PackContents pack)
    {
        var problems = new List<string>();
        foreach (var action in pack.Actions)
        {
            foreach (var pair in action.Effects.OrderBy(entry => entry.Key.Value, CodePointComparer.Instance))
            {
                if (pair.Value < 0.0 && !action.Costs.ContainsKey(pair.Key))
                {
                    problems.Add($"acción {PythonText.Repr(action.Id.Value)}: el efecto sobre {PythonText.Repr(pair.Key.Value)} es negativo ({PythonText.Float(pair.Value)}) pero no declara coste. Añádelo a `costs`, o pon {pair.Key.Value}: 0.0 si el agente debe ignorarlo a propósito");
                }
            }
        }

        return problems;
    }

    /// <summary>Comprobación 12: una precondición no puede quedarse por debajo de lo que la acción consume.</summary>
    /// <param name="pack">El contenido leído.</param>
    /// <returns>Los mensajes, en el orden de las acciones y de sus necesidades.</returns>
    internal static List<string> RequirementsCoverWhatTheySpend(PackContents pack)
    {
        var problems = new List<string>();
        foreach (var action in pack.Actions)
        {
            foreach (var pair in action.Requires.OrderBy(entry => entry.Key.Value, CodePointComparer.Instance))
            {
                var spend = -EffectOf(action, pair.Key);
                if (spend > 0.0 && pair.Value < spend)
                {
                    problems.Add($"acción {PythonText.Repr(action.Id.Value)}: exige {PythonText.Float(pair.Value)} de {PythonText.Repr(pair.Key.Value)} pero puede consumir hasta {PythonText.Float(PythonText.Round4(spend))}. Un agente justo por encima del mínimo seguiría truncando el efecto; sube el requisito o quítalo");
                }
            }
        }

        return problems;
    }

    /// <summary>Comprobación 13: cada acción puede ejecutarse al menos una vez por algún agente.</summary>
    /// <param name="pack">El contenido leído.</param>
    /// <returns>Los mensajes, en el orden de las acciones.</returns>
    internal static List<string> ActionsAreReachable(PackContents pack)
    {
        var moved = MovedBy(pack, action => action.Effects);
        var needDefaults = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var need in pack.Needs)
        {
            needDefaults[need.Id.Value] = need.Initial;
        }

        var problems = new List<string>();
        foreach (var action in pack.Actions)
        {
            var name = $"acción {PythonText.Repr(action.Id.Value)}";
            foreach (var pair in action.Requires.OrderBy(entry => entry.Key.Value, CodePointComparer.Instance))
            {
                var need = PythonText.Repr(pair.Key.Value);
                var amount = PythonText.Float(pair.Value);
                if (pair.Value > 1.0)
                {
                    problems.Add($"{name}: exige {amount} de {need}, pero el valor máximo de una necesidad es 1.0; jamás será ejecutable");
                }
                else if (!moved.Contains(pair.Key.Value) && pack.Agents.Length > 0
                    && !StartsWithEnough(pack, needDefaults, pair.Key, pair.Value))
                {
                    problems.Add($"{name}: exige {amount} de {need}, pero ningún agente empieza con ese nivel y ninguna acción lo sube; jamás será ejecutable");
                }
            }

            foreach (var pair in action.RequiresTraits.OrderBy(entry => entry.Key.Value, CodePointComparer.Instance))
            {
                if (pack.Agents.Length > 0 && !SomeAgentReaches(pack, pair.Key, pair.Value))
                {
                    problems.Add($"{name}: exige rasgo {PythonText.Repr(pair.Key.Value)} a {PythonText.Float(pair.Value)}, pero ningún agente lo alcanza ni tiene metas que lo suban; jamás será ejecutable");
                }
            }
        }

        return problems;
    }

    // Algún agente empieza con ese nivel: su valor inicial, o el de la necesidad, o 0.0 si la necesidad no existe.
    private static bool StartsWithEnough(PackContents pack, Dictionary<string, double> needDefaults, NeedId need, double minimum) =>
        pack.Agents.Any(agent =>
        {
            var start = agent.InitialNeeds.TryGetValue(need, out var own) ? own : needDefaults.TryGetValue(need.Value, out var fallback) ? fallback : 0.0;
            return start >= minimum;
        });

    // Un agente llega al rasgo si lo tiene más lo que sus metas le suben en total, sumado en el orden del YAML.
    private static bool SomeAgentReaches(PackContents pack, TraitId trait, double required) =>
        pack.Agents.Any(agent =>
        {
            var upkeep = 0.0;
            foreach (var goal in agent.Goals)
            {
                upkeep += PythonMax(DriftOf(goal.OnSuccess, trait)) + PythonMax(DriftOf(goal.OnFailure, trait));
            }

            var has = agent.Traits.TryGetValue(trait, out var value) ? value : 0.0;
            return has + upkeep >= required;
        });

    private static double DriftOf(DeclaredMap<TraitId, double> drifts, TraitId trait) =>
        drifts.TryGetValue(trait, out var value) ? value : 0.0;

    private static double EffectOf(ActionSpec action, NeedId need) =>
        action.Effects.TryGetValue(need, out var value) ? value : 0.0;

    // Lo que suben las acciones: las necesidades con un valor mayor que 0 en el mapa que se indique.
    private static HashSet<string> MovedBy(PackContents pack, Func<ActionSpec, DeclaredMap<NeedId, double>> map) =>
        pack.Actions
            .SelectMany(action => map(action).Where(entry => entry.Value > 0.0).Select(entry => entry.Key.Value))
            .ToHashSet(StringComparer.Ordinal);

    // max(x, 0.0) de Python: un NaN se conserva.
    private static double PythonMax(double value) => 0.0 > value ? 0.0 : value;
}
