using Aida.Engine.Serialization;

namespace Aida.Engine.Packs.Checks;

/// <summary>Comprobaciones 3, 4, 5, 6 y 10: toda referencia a una necesidad, un rasgo, una franja, una situación o un gusto existe.</summary>
/// <remarks>Las referencias de una meta (<c>at_traits</c>, <c>need</c>) no están aquí: viven en la comprobación 7.</remarks>
internal static class ReferenceChecks
{
    /// <summary>Comprobación 3: las necesidades referidas existen (con <c>_unknown_keys</c>).</summary>
    internal static List<string> NeedRefs(PackContents pack)
    {
        var known = pack.Needs.Select(need => need.Id.Value).ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var trait in pack.Traits)
        {
            var name = "rasgo " + PythonText.Repr(trait.Id.Value);
            problems.AddRange(UnknownKeys(name, "necesidad", trait.NeedWeightMultipliers.Keys.Select(key => key.Value), known));
            problems.AddRange(UnknownKeys(name + ".gain_multipliers", "necesidad", trait.GainMultipliers.Keys.Select(key => key.Value), known));
            problems.AddRange(UnknownKeys(name + ".cost_multipliers", "necesidad", trait.CostMultipliers.Keys.Select(key => key.Value), known));
            problems.AddRange(UnknownKeys(name + ".cap_multipliers", "necesidad", trait.CapMultipliers.Keys.Select(key => key.Value), known));
        }

        foreach (var action in pack.Actions)
        {
            var name = "acción " + PythonText.Repr(action.Id.Value);
            problems.AddRange(UnknownKeys(name + ".satisfies", "necesidad", action.Satisfies.Keys.Select(key => key.Value), known));
            problems.AddRange(UnknownKeys(name + ".costs", "necesidad", action.Costs.Keys.Select(key => key.Value), known));
            problems.AddRange(UnknownKeys(name + ".requires", "necesidad", action.Requires.Keys.Select(key => key.Value), known));
            problems.AddRange(UnknownKeys(name + ".effects", "necesidad", action.Effects.Keys.Select(key => key.Value), known));
        }

        foreach (var situation in pack.Situations)
        {
            var name = "situación " + PythonText.Repr(situation.Id.Value);
            problems.AddRange(UnknownKeys(name + ".gain_multipliers", "necesidad", situation.GainMultipliers.Keys.Select(key => key.Value), known));
            problems.AddRange(UnknownKeys(name + ".effects", "necesidad", situation.Effects.Keys.Select(key => key.Value), known));
        }

        foreach (var agent in pack.Agents)
        {
            problems.AddRange(UnknownKeys("agente " + PythonText.Repr(agent.Id.Value) + ".initial_needs", "necesidad",
                agent.InitialNeeds.Keys.Select(key => key.Value), known));
        }

        foreach (var intent in pack.Intents)
        {
            if (!known.Contains(intent.Need.Value))
            {
                problems.Add("intención " + PythonText.Repr(intent.Id.Value) + ": necesidad desconocida " + PythonText.Repr(intent.Need.Value));
            }
        }

        return problems;
    }

    /// <summary>Comprobación 4: los rasgos referidos por agentes y acciones existen.</summary>
    internal static List<string> TraitRefs(PackContents pack)
    {
        var known = pack.Traits.Select(trait => trait.Id.Value).ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var agent in pack.Agents)
        {
            problems.AddRange(UnknownKeys("agente " + PythonText.Repr(agent.Id.Value), "rasgo", agent.Traits.Keys.Select(key => key.Value), known));
        }

        foreach (var action in pack.Actions)
        {
            problems.AddRange(UnknownKeys("acción " + PythonText.Repr(action.Id.Value) + ".requires_traits", "rasgo",
                action.RequiresTraits.Keys.Select(key => key.Value), known));
        }

        return problems;
    }

    /// <summary>Comprobación 5: las franjas referidas por acciones existen, y un desfase exige que haya franjas.</summary>
    internal static List<string> PhaseRefs(PackContents pack)
    {
        var known = pack.Phases.Select(phase => phase.Id.Value).ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var action in pack.Actions)
        {
            problems.AddRange(UnknownKeys("acción " + PythonText.Repr(action.Id.Value), "franja",
                action.PhaseMultipliers.Keys.Select(key => key.Value), known));
        }

        foreach (var trait in pack.Traits)
        {
            if (trait.PhaseShift != 0 && known.Count == 0)
            {
                problems.Add("rasgo " + PythonText.Repr(trait.Id.Value) + ": declara `phase_shift` pero el pack no "
                    + "tiene franjas, así que el desfase no puede hacer nada");
            }
        }

        return problems;
    }

    /// <summary>Comprobación 6: las situaciones referidas por acciones existen.</summary>
    internal static List<string> SituationRefs(PackContents pack)
    {
        var known = pack.Situations.Select(situation => situation.Id.Value).ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var action in pack.Actions)
        {
            problems.AddRange(UnknownKeys("acción " + PythonText.Repr(action.Id.Value), "situación",
                action.SituationMultipliers.Keys.Select(key => key.Value), known));
        }

        return problems;
    }

    /// <summary>Comprobación 10: todo gusto apunta a una acción o a un agente que existe.</summary>
    internal static List<string> TasteRefs(PackContents pack)
    {
        var known = pack.Actions.Select(action => action.Id.Value)
            .Concat(pack.Agents.Select(agent => agent.Id.Value))
            .ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var agent in pack.Agents)
        {
            foreach (var taste in agent.Tastes)
            {
                if (!known.Contains(taste.Target))
                {
                    problems.Add("agente " + PythonText.Repr(agent.Id.Value) + ": gusto hacia " + PythonText.Repr(taste.Target)
                        + ", que no es ninguna acción ni agente");
                }
            }
        }

        return problems;
    }

    /// <summary>El <c>_unknown_keys</c> de la 0.1: las claves que no están en <paramref name="known"/>, en orden de punto de código.</summary>
    /// <param name="context">El contexto del mensaje.</param>
    /// <param name="kind">El nombre de lo que falta, en plural o singular como en la 0.1.</param>
    /// <param name="keys">Las claves del mapa.</param>
    /// <param name="known">Lo que existe.</param>
    /// <returns>Un mensaje por clave desconocida.</returns>
    internal static List<string> UnknownKeys(string context, string kind, IEnumerable<string> keys, HashSet<string> known) =>
        keys.Distinct(StringComparer.Ordinal)
            .Where(key => !known.Contains(key))
            .OrderBy(key => key, CodePointComparer.Instance)
            .Select(key => context + ": " + kind + " desconocida/o " + PythonText.Repr(key))
            .ToList();
}
