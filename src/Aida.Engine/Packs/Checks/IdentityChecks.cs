using Aida.Engine.Serialization;

namespace Aida.Engine.Packs.Checks;

/// <summary>Comprobaciones 1 y 2: identificadores repetidos y colisiones entre acciones y agentes (<c>pack.py:148-178</c>).</summary>
internal static class IdentityChecks
{
    /// <summary>Comprobación 1: ningún identificador repetido dentro de una colección.</summary>
    /// <param name="pack">El contenido leído.</param>
    /// <returns>Los mensajes, en el orden de la 0.1.</returns>
    internal static List<string> UniqueIds(PackContents pack)
    {
        var problems = new List<string>();
        problems.AddRange(Duplicates("necesidades", pack.Needs.Select(need => need.Id.Value)));
        problems.AddRange(Duplicates("rasgos", pack.Traits.Select(trait => trait.Id.Value)));
        problems.AddRange(Duplicates("acciones", pack.Actions.Select(action => action.Id.Value)));
        problems.AddRange(Duplicates("agentes", pack.Agents.Select(agent => agent.Id.Value)));
        problems.AddRange(Duplicates("franjas", pack.Phases.Select(phase => phase.Id.Value)));
        problems.AddRange(Duplicates("situaciones", pack.Situations.Select(situation => situation.Id.Value)));
        problems.AddRange(Duplicates("metas", pack.Goals.Select(goal => goal.Id.Value)));
        problems.AddRange(Duplicates("intenciones", pack.Intents.Select(intent => intent.Id.Value)));
        foreach (var agent in pack.Agents)
        {
            problems.AddRange(Duplicates($"metas de {PythonText.Repr(agent.Id.Value)}", agent.Goals.Select(goal => goal.Id.Value)));
        }

        return problems;
    }

    /// <summary>Comprobación 2: una acción y un agente no pueden compartir identificador.</summary>
    /// <param name="pack">El contenido leído.</param>
    /// <returns>Los mensajes, en orden de identificador.</returns>
    internal static List<string> GlobalIdCollisions(PackContents pack)
    {
        var actionIds = pack.Actions.Select(action => action.Id.Value).ToHashSet(StringComparer.Ordinal);
        var agentIds = pack.Agents.Select(agent => agent.Id.Value).ToHashSet(StringComparer.Ordinal);
        return actionIds.Intersect(agentIds, StringComparer.Ordinal)
            .OrderBy(clash => clash, CodePointComparer.Instance)
            .Select(clash => $"id {PythonText.Repr(clash)} usado a la vez por un/a acción y un/a agente: los gustos no podrían distinguirlos")
            .ToList();
    }

    // Counter(ids) y sorted(counts.items()): cada repetido, en orden de punto de código, con sus apariciones.
    private static IEnumerable<string> Duplicates(string kind, IEnumerable<string> ids) =>
        ids.GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, CodePointComparer.Instance)
            .Select(group => $"{kind}: id duplicado {PythonText.Repr(group.Key)} (aparece {group.Count().ToString(System.Globalization.CultureInfo.InvariantCulture)} veces)");
}
