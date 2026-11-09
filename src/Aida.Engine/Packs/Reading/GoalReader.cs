using Aida.Engine.Ontology;
using Aida.Engine.Serialization;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Lee una meta (<see cref="GoalSpec"/>) y sus dos comprobaciones propias (§A.9).</summary>
/// <remarks>
/// La primera comprobación es la de la condición única y la de la meta que se abre a sí misma. Si falla, la segunda
/// (las derivas) no corre: pydantic corta la cadena (<c>ontology.py:529-589</c>).
/// </remarks>
internal static class GoalReader
{
    /// <summary>Lee una meta.</summary>
    /// <param name="node">El nodo, que debe ser un mapa.</param>
    /// <param name="path">La ruta de la definición.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    /// <param name="value">La meta, si es válida.</param>
    /// <returns><see langword="true"/> si la definición es válida.</returns>
    internal static bool TryRead(YamlNode node, string path, ProblemList problems, out GoalSpec value)
    {
        value = null!;
        var before = problems.Count;
        if (!FieldReader.TryMapping(node, path, problems, out var mapping))
        {
            return false;
        }

        var fields = new MappingFields(mapping, path, problems);
        var id = fields.Required<GoalId>("id", Ids.Goal);
        var label = fields.Required<string>("label", FieldReader.TryText);
        var need = fields.Required<NeedId>("need", Ids.Need);
        var atLeast = fields.Optional<double?>("at_least", FieldReader.Optional<double>(Constraints.FloatInHalfOpen(0.0, 1.0)), null);
        var atTraits = fields.Optional("at_traits", CollectionReader.Map<TraitId, double>(Ids.Trait, FieldReader.TryFloat), DeclaredMap<TraitId, double>.Empty);
        var requiresGoal = fields.Optional<GoalId?>("requires_goal", FieldReader.Optional<GoalId>(Ids.Goal), null);
        var weight = fields.Optional<double>("weight_multiplier", Constraints.FloatAbove(0.0), 2.0);
        var deadline = fields.Optional<long?>("deadline", FieldReader.Optional<long>(Constraints.IntAtLeast(1)), null);
        var onSuccess = fields.Optional("on_success", CollectionReader.Map<TraitId, double>(Ids.Trait, FieldReader.TryFloat), DeclaredMap<TraitId, double>.Empty);
        var onFailure = fields.Optional("on_failure", CollectionReader.Map<TraitId, double>(Ids.Trait, FieldReader.TryFloat), DeclaredMap<TraitId, double>.Empty);
        fields.RejectExtras();
        if (problems.Count != before)
        {
            return false;
        }

        if (!ValidateCondition(id, atLeast, atTraits, requiresGoal, path, problems) || !ValidateDrifts(id, onSuccess, onFailure, path, problems))
        {
            return false;
        }

        value = new GoalSpec(id, label, need, atLeast, atTraits, requiresGoal, weight, deadline, onSuccess, onFailure);
        return true;
    }

    // ontology.py:530-546: exactamente una condición, y una meta no se abre a sí misma.
    private static bool ValidateCondition(GoalId id, double? atLeast, DeclaredMap<TraitId, double> atTraits,
        GoalId? requiresGoal, string path, ProblemList problems)
    {
        var count = (atLeast is null ? 0 : 1) + (atTraits.Count > 0 ? 1 : 0);
        if (count != 1)
        {
            problems.Add(path, $"meta {PythonText.Repr(id.Value)}: hace falta EXACTAMENTE una condición de `at_least` (el valor del eje) o `at_traits` (ser quien hay que ser), y hay {count}", fromAida: true);
            return false;
        }

        if (requiresGoal == id)
        {
            problems.Add(path, $"meta {PythonText.Repr(id.Value)}: se abre a sí misma", fromAida: true);
            return false;
        }

        return true;
    }

    // ontology.py:575-589: una deriva fuera de [-2, 2] no significa nada; se recorre en orden de rasgo.
    private static bool ValidateDrifts(GoalId id, DeclaredMap<TraitId, double> onSuccess, DeclaredMap<TraitId, double> onFailure,
        string path, ProblemList problems)
    {
        foreach (var (field, drifts) in new[] { ("on_success", onSuccess), ("on_failure", onFailure) })
        {
            foreach (var pair in drifts.OrderBy(entry => entry.Key.Value, CodePointComparer.Instance))
            {
                if (!(pair.Value >= -2.0 && pair.Value <= 2.0))
                {
                    problems.Add(path, $"meta {PythonText.Repr(id.Value)}.{field}[{PythonText.Repr(pair.Key.Value)}] = {PythonText.Float(pair.Value)}, fuera de [-2, 2]; los rasgos viven en [-1, 1] y un 2 ya cruza el rango entero", fromAida: true);
                    return false;
                }
            }
        }

        return true;
    }
}
