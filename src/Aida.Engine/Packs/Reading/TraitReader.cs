using Aida.Engine.Ontology;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Lee un rasgo (<see cref="TraitSpec"/>). No tiene comprobación propia: sus referencias las mira el pack entero.</summary>
internal static class TraitReader
{
    /// <summary>Lee un rasgo.</summary>
    /// <param name="node">El nodo, que debe ser un mapa.</param>
    /// <param name="path">La ruta de la definición.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    /// <param name="value">El rasgo, si es válido.</param>
    /// <returns><see langword="true"/> si la definición es válida.</returns>
    internal static bool TryRead(YamlNode node, string path, ProblemList problems, out TraitSpec value)
    {
        value = null!;
        var before = problems.Count;
        if (!FieldReader.TryMapping(node, path, problems, out var mapping))
        {
            return false;
        }

        var fields = new MappingFields(mapping, path, problems);
        var id = fields.Required<TraitId>("id", Ids.Trait);
        var label = fields.Required<string>("label", FieldReader.TryText);
        var needWeight = fields.Optional("need_weight_multipliers", CollectionReader.Map(Ids.Need, Constraints.FloatAbove(0.0)), DeclaredMap<NeedId, double>.Empty);
        var gain = fields.Optional("gain_multipliers", CollectionReader.Map(Ids.Need, Constraints.FloatAbove(0.0)), DeclaredMap<NeedId, double>.Empty);
        var cost = fields.Optional("cost_multipliers", CollectionReader.Map(Ids.Need, Constraints.FloatAbove(0.0)), DeclaredMap<NeedId, double>.Empty);
        var cap = fields.Optional("cap_multipliers", CollectionReader.Map(Ids.Need, Constraints.FloatAbove(0.0)), DeclaredMap<NeedId, double>.Empty);
        var phaseShift = fields.Optional<long>("phase_shift", FieldReader.TryInt, 0);
        var temperament = fields.Optional("temperament_multipliers",
            CollectionReader.Map<TemperamentKnob, double>(FieldReader.TryTemperamentKnob, Constraints.FloatAbove(0.0)), DeclaredMap<TemperamentKnob, double>.Empty);
        fields.RejectExtras();
        if (problems.Count != before)
        {
            return false;
        }

        value = new TraitSpec(id, label, needWeight, gain, cost, cap, phaseShift, temperament);
        return true;
    }
}
