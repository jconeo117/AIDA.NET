using System.Collections.Immutable;
using Aida.Engine.Ontology;
using Aida.Engine.Serialization;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Lee un agente (<see cref="AgentSpec"/>), sus gustos, y la comprobación de rango de sus rasgos (§A.9).</summary>
/// <remarks>
/// Un gusto (<see cref="TasteSpec"/>) no tiene comprobación propia; vive aquí porque solo lo lee el agente.
/// </remarks>
internal static class AgentReader
{
    /// <summary>Lee un agente.</summary>
    /// <param name="node">El nodo, que debe ser un mapa.</param>
    /// <param name="path">La ruta de la definición.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    /// <param name="value">El agente, si es válido.</param>
    /// <returns><see langword="true"/> si la definición es válida.</returns>
    internal static bool TryRead(YamlNode node, string path, ProblemList problems, out AgentSpec value)
    {
        value = null!;
        var before = problems.Count;
        if (!FieldReader.TryMapping(node, path, problems, out var mapping))
        {
            return false;
        }

        var fields = new MappingFields(mapping, path, problems);
        var id = fields.Required<AgentId>("id", Ids.Agent);
        var label = fields.Required<string>("label", FieldReader.TryText);
        var goals = fields.Optional("goals", CollectionReader.Items<GoalSpec>(GoalReader.TryRead), ImmutableArray<GoalSpec>.Empty);
        var traits = fields.Optional("traits", CollectionReader.Map<TraitId, double>(Ids.Trait, FieldReader.TryFloat), DeclaredMap<TraitId, double>.Empty);
        var tastes = fields.Optional("tastes", CollectionReader.Items<TasteSpec>(TryReadTaste), ImmutableArray<TasteSpec>.Empty);
        var initialNeeds = fields.Optional("initial_needs", CollectionReader.Map<NeedId, double>(Ids.Need, FieldReader.TryFloat), DeclaredMap<NeedId, double>.Empty);
        fields.RejectExtras();
        if (problems.Count != before)
        {
            return false;
        }

        if (!ValidateTraitRange(traits, path, problems))
        {
            return false;
        }

        value = new AgentSpec(id, label, goals, traits, tastes, initialNeeds);
        return true;
    }

    /// <summary>Lee un gusto: su destino (texto) y su multiplicador (mayor que 0).</summary>
    /// <param name="node">El nodo, que debe ser un mapa.</param>
    /// <param name="path">La ruta del gusto.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    /// <param name="value">El gusto, si es válido.</param>
    /// <returns><see langword="true"/> si el gusto es válido.</returns>
    private static bool TryReadTaste(YamlNode node, string path, ProblemList problems, out TasteSpec value)
    {
        value = null!;
        var before = problems.Count;
        if (!FieldReader.TryMapping(node, path, problems, out var mapping))
        {
            return false;
        }

        var fields = new MappingFields(mapping, path, problems);
        var target = fields.Required<string>("target", FieldReader.TryText);
        var multiplier = fields.Required<double>("multiplier", Constraints.FloatAbove(0.0));
        fields.RejectExtras();
        if (problems.Count != before)
        {
            return false;
        }

        value = new TasteSpec(target, multiplier);
        return true;
    }

    // ontology.py:626-639: fuera de [-1, 1] la intensidad no significa nada; se recorre en orden de rasgo.
    private static bool ValidateTraitRange(DeclaredMap<TraitId, double> traits, string path, ProblemList problems)
    {
        foreach (var pair in traits.OrderBy(entry => entry.Key.Value, CodePointComparer.Instance))
        {
            if (!(pair.Value >= -1.0 && pair.Value <= 1.0))
            {
                problems.Add(path, "rasgo " + PythonText.Repr(pair.Key.Value) + " = " + PythonText.Float(pair.Value) + ", fuera de [-1, 1]", fromAida: true);
                return false;
            }
        }

        return true;
    }
}
