using Aida.Engine.Ontology;
using Aida.Engine.Serialization;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Lee una necesidad (<see cref="NeedSpec"/>) y su comprobación propia (§A.9).</summary>
/// <remarks>Los campos siguen el orden de la tabla de §A.3; si alguno falla, la comprobación propia no corre.</remarks>
internal static class NeedReader
{
    /// <summary>Lee una necesidad.</summary>
    /// <param name="node">El nodo, que debe ser un mapa.</param>
    /// <param name="path">La ruta de la definición.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    /// <param name="value">La necesidad, si es válida.</param>
    /// <returns><see langword="true"/> si la definición es válida.</returns>
    internal static bool TryRead(YamlNode node, string path, ProblemList problems, out NeedSpec value)
    {
        value = null!;
        var before = problems.Count;
        if (!FieldReader.TryMapping(node, path, problems, out var mapping))
        {
            return false;
        }

        var fields = new MappingFields(mapping, path, problems);
        var id = fields.Required<NeedId>("id", FieldReader.Id(text => new NeedId(text)));
        var label = fields.Required<string>("label", FieldReader.TryText);
        var decay = fields.Required<double>("decay_per_tick", Constraints.FloatIn(0.0, 1.0));
        var progression = fields.Optional<bool>("progression", FieldReader.TryBool, false);
        var urgentWhenLow = fields.Optional<bool>("urgent_when_low", FieldReader.TryBool, true);
        var initial = fields.Optional<double>("initial", Constraints.FloatIn(0.0, 1.0), 1.0);
        var baseWeight = fields.Optional<double>("base_weight", Constraints.FloatAtLeast(0.0), 1.0);
        var strainWeight = fields.Optional<double>("strain_weight", Constraints.FloatAtLeast(0.0), 0.0);
        var escalatesAfter = fields.Optional<long>("escalates_after", Constraints.IntAtLeast(0), 0);
        var maxDebt = fields.Optional<double>("max_debt", Constraints.FloatAtLeast(0.0), 0.0);
        var urgencyExponent = fields.Optional<double>("urgency_exponent", Constraints.FloatAbove(0.0), 2.0);
        var cap = fields.Optional<double>("cap", Constraints.FloatInHalfOpen(0.0, 1.0), 1.0);
        fields.RejectExtras();
        if (problems.Count != before)
        {
            return false;
        }

        if (progression && !ValidateProgression(id, decay, maxDebt, strainWeight, path, problems))
        {
            return false;
        }

        value = new NeedSpec(id, label, decay, progression, urgentWhenLow, initial, baseWeight, strainWeight,
            escalatesAfter, maxDebt, urgencyExponent, cap);
        return true;
    }

    // Un eje de progresión no admite las mecánicas de la carencia (ontology.py:221-244). Los campos vetados que
    // valen algo se nombran en orden alfabético, como hace sorted() de Python.
    private static bool ValidateProgression(NeedId id, double decay, double maxDebt, double strainWeight, string path, ProblemList problems)
    {
        var vetoed = new SortedDictionary<string, double>(StringComparer.Ordinal)
        {
            ["decay_per_tick"] = decay,
            ["max_debt"] = maxDebt,
            ["strain_weight"] = strainWeight,
        };
        var set = vetoed.Where(pair => pair.Value != 0.0).Select(pair => pair.Key).ToList();
        if (set.Count == 0)
        {
            return true;
        }

        problems.Add(path, $"eje de progresión {PythonText.Repr(id.Value)}: {string.Join(", ", set)} no significa nada en algo que solo sube", fromAida: true);
        return false;
    }
}
