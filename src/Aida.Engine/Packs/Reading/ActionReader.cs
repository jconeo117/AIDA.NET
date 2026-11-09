using System.Collections.Immutable;
using System.Globalization;
using Aida.Engine.Ontology;
using Aida.Engine.Serialization;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Lee una acción (<see cref="ActionSpec"/>) y su comprobación de coherencia de signos y cotas (§A.9).</summary>
/// <remarks>
/// Las etiquetas (<c>frozenset</c> en la 0.1) se leen sin repetidos y ordenadas por punto de código, igual que las
/// vuelca la 0.1. La comprobación recorre sus tres bloques en orden y corta en el primer problema.
/// </remarks>
internal static class ActionReader
{
    /// <summary>Lee una acción.</summary>
    /// <param name="node">El nodo, que debe ser un mapa.</param>
    /// <param name="path">La ruta de la definición.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    /// <param name="value">La acción, si es válida.</param>
    /// <returns><see langword="true"/> si la definición es válida.</returns>
    internal static bool TryRead(YamlNode node, string path, ProblemList problems, out ActionSpec value)
    {
        value = null!;
        var before = problems.Count;
        if (!FieldReader.TryMapping(node, path, problems, out var mapping))
        {
            return false;
        }

        var fields = new MappingFields(mapping, path, problems);
        var id = fields.Required<ActionId>("id", Ids.Action);
        var label = fields.Required<string>("label", FieldReader.TryText);
        var reversibility = fields.Required<Reversibility>("reversibility", FieldReader.TryReversibility);
        var duration = fields.Required<long>("duration_ticks", Constraints.IntAtLeast(1));
        var interruptible = fields.Optional<bool>("interruptible", FieldReader.TryBool, true);
        var requires = fields.Optional("requires", CollectionReader.Map<NeedId, double>(Ids.Need, Constraints.FloatAtLeast(0.0)), DeclaredMap<NeedId, double>.Empty);
        var requiresTraits = fields.Optional("requires_traits", CollectionReader.Map<TraitId, double>(Ids.Trait, FieldReader.TryFloat), DeclaredMap<TraitId, double>.Empty);
        var satisfies = fields.Optional("satisfies", CollectionReader.Map<NeedId, double>(Ids.Need, FieldReader.TryFloat), DeclaredMap<NeedId, double>.Empty);
        var costs = fields.Optional("costs", CollectionReader.Map<NeedId, double>(Ids.Need, Constraints.FloatAtLeast(0.0)), DeclaredMap<NeedId, double>.Empty);
        var effects = fields.Optional("effects", CollectionReader.Map<NeedId, double>(Ids.Need, FieldReader.TryFloat), DeclaredMap<NeedId, double>.Empty);
        var phaseMultipliers = fields.Optional("phase_multipliers", CollectionReader.Map<PhaseId, double>(Ids.Phase, Constraints.FloatAbove(0.0)), DeclaredMap<PhaseId, double>.Empty);
        var situationMultipliers = fields.Optional("situation_multipliers", CollectionReader.Map<SituationId, double>(Ids.Situation, Constraints.FloatAbove(0.0)), DeclaredMap<SituationId, double>.Empty);
        var saturationGain = fields.Optional<double>("saturation_gain", Constraints.FloatAtLeast(0.0), 0.0);
        var tags = fields.Optional<ImmutableArray<Tag>>("tags", TagsOf, ImmutableArray<Tag>.Empty);
        fields.RejectExtras();
        if (problems.Count != before)
        {
            return false;
        }

        if (!ValidateCoherence(id, requiresTraits, satisfies, costs, effects, path, problems))
        {
            return false;
        }

        value = new ActionSpec(id, label, reversibility, duration, interruptible, requires, requiresTraits, satisfies, costs,
            effects, phaseMultipliers, situationMultipliers, saturationGain, tags);
        return true;
    }

    // frozenset[Tag] de la 0.1: sin repetidos y en orden de punto de código (ontology.py:892-898).
    private static bool TagsOf(YamlNode node, string path, ProblemList problems, out ImmutableArray<Tag> value)
    {
        if (!CollectionReader.Items<Tag>(Ids.TagLabel)(node, path, problems, out var items))
        {
            value = ImmutableArray<Tag>.Empty;
            return false;
        }

        value = items.Distinct().OrderBy(tag => tag.Value, CodePointComparer.Instance).ToImmutableArray();
        return true;
    }

    // ontology.py:901-927. Cada bloque recorre sus claves en orden de punto de código; corta en el primer problema.
    private static bool ValidateCoherence(ActionId id, DeclaredMap<TraitId, double> requiresTraits,
        DeclaredMap<NeedId, double> satisfies, DeclaredMap<NeedId, double> costs, DeclaredMap<NeedId, double> effects,
        string path, ProblemList problems)
    {
        var name = PythonText.Repr(id.Value);
        foreach (var pair in requiresTraits.OrderBy(entry => entry.Key.Value, CodePointComparer.Instance))
        {
            if (!(pair.Value >= -1.0 && pair.Value <= 1.0))
            {
                problems.Add(path, "acción " + name + ".requires_traits[" + PythonText.Repr(pair.Key.Value) + "] = " + PythonText.Float(pair.Value) + ", fuera de [-1, 1]", fromAida: true);
                return false;
            }
        }

        foreach (var need in satisfies.Keys.OrderBy(key => key.Value, CodePointComparer.Instance))
        {
            var found = effects.TryGetValue(need, out var effect);
            if (!found || effect <= 0.0)
            {
                problems.Add(path, "acción " + name + ": la clave " + PythonText.Repr(need.Value) + " de `satisfies` debe aparecer en `effects` con signo positivo (> 0.0), pero vale " + Describe(found, effect), fromAida: true);
                return false;
            }
        }

        foreach (var need in costs.Keys.OrderBy(key => key.Value, CodePointComparer.Instance))
        {
            var found = effects.TryGetValue(need, out var effect);
            if (!found || effect >= 0.0)
            {
                problems.Add(path, "acción " + name + ": la clave " + PythonText.Repr(need.Value) + " de `costs` debe aparecer en `effects` con signo negativo (< 0.0), pero vale " + Describe(found, effect), fromAida: true);
                return false;
            }
        }

        return true;
    }

    // El {effect!r} de Python: None si la clave no está, y el decimal en su repr si está.
    private static string Describe(bool found, double effect) => found ? PythonText.Float(effect) : "None";
}
