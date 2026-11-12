using System.Collections.Immutable;
using Aida.Engine.Ontology;
using Aida.Engine.Serialization;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Lee los tres bloques de ajustes del pack: autoridad, salud y ajustes finos (sin comprobación propia).</summary>
/// <remarks>
/// Cuando un bloque falta, pydantic usa el valor por defecto de su clase: por eso cada bloque expone su
/// <c>Default</c>. Las comprobaciones de salud y de cotas del pack entero van en <c>SettingsChecks</c>.
/// </remarks>
internal static class SettingsReader
{
    /// <summary>La autoridad por defecto de un pack que no la declara.</summary>
    internal static AuthoritySpec DefaultAuthority() => new(Reversibility.Reversible, 48);

    /// <summary>La salud por defecto de un pack que no la declara.</summary>
    internal static HealthSpec DefaultHealth() => new(7, [1312, 7, 99], null, null, null, null, ImmutableArray<ActionId>.Empty);

    /// <summary>Los ajustes finos por defecto de un pack que no los declara.</summary>
    internal static TuningSpec DefaultTuning() => new(3, 0.15, 0.004, 0.6, 1.0, 0.0, DeclaredMap<TemperamentKnob, double>.Empty, 1.0, 0.0, 0.15, 0.35);

    /// <summary>Lee la autoridad.</summary>
    /// <param name="node">El nodo, que debe ser un mapa.</param>
    /// <param name="path">La ruta del bloque.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    /// <param name="value">La autoridad, si es válida.</param>
    /// <returns><see langword="true"/> si el bloque es válido.</returns>
    internal static bool TryReadAuthority(YamlNode node, string path, ProblemList problems, out AuthoritySpec value)
    {
        value = null!;
        var before = problems.Count;
        if (!FieldReader.TryMapping(node, path, problems, out var mapping))
        {
            return false;
        }

        var fields = new MappingFields(mapping, path, problems);
        var autonomous = fields.Optional<Reversibility>("autonomous_up_to", FieldReader.TryReversibility, Reversibility.Reversible);
        var ttl = fields.Optional<long>("confirmation_ttl_ticks", Constraints.IntAtLeast(1), 48);
        fields.RejectExtras();
        if (problems.Count != before)
        {
            return false;
        }

        value = new AuthoritySpec(autonomous, ttl);
        return true;
    }

    /// <summary>Lee la salud del pack.</summary>
    /// <param name="node">El nodo, que debe ser un mapa.</param>
    /// <param name="path">La ruta del bloque.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    /// <param name="value">La salud, si es válida.</param>
    /// <returns><see langword="true"/> si el bloque es válido.</returns>
    internal static bool TryReadHealth(YamlNode node, string path, ProblemList problems, out HealthSpec value)
    {
        value = null!;
        var before = problems.Count;
        if (!FieldReader.TryMapping(node, path, problems, out var mapping))
        {
            return false;
        }

        var fields = new MappingFields(mapping, path, problems);
        var days = fields.Optional<long>("days", Constraints.IntAtLeast(1), 7);
        var seeds = fields.Optional<ImmutableArray<long>>("seeds", CollectionReader.Items<long>(FieldReader.TryInt), [1312, 7, 99]);
        var minCrisis = fields.Optional<double?>("min_crisis", FieldReader.Optional<double>(Constraints.FloatIn(0.0, 1.0)), null);
        var maxCrisis = fields.Optional<double?>("max_crisis", FieldReader.Optional<double>(Constraints.FloatIn(0.0, 1.0)), null);
        var maxAbort = fields.Optional<double?>("max_abort_ratio", FieldReader.Optional<double>(Constraints.FloatAtLeast(0.0)), null);
        var minDifferentiation = fields.Optional<double?>("min_differentiation", FieldReader.Optional<double>(Constraints.FloatAtLeast(0.0)), null);
        var mayGoUnused = fields.Optional<ImmutableArray<ActionId>>("may_go_unused", SortedUnique(Ids.Action, action => action.Value), ImmutableArray<ActionId>.Empty);
        fields.RejectExtras();
        if (problems.Count != before)
        {
            return false;
        }

        value = new HealthSpec(days, seeds, minCrisis, maxCrisis, maxAbort, minDifferentiation, mayGoUnused);
        return true;
    }

    /// <summary>Lee los ajustes finos del motor.</summary>
    /// <param name="node">El nodo, que debe ser un mapa.</param>
    /// <param name="path">La ruta del bloque.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    /// <param name="value">Los ajustes, si son válidos.</param>
    /// <returns><see langword="true"/> si el bloque es válido.</returns>
    internal static bool TryReadTuning(YamlNode node, string path, ProblemList problems, out TuningSpec value)
    {
        value = null!;
        var before = problems.Count;
        if (!FieldReader.TryMapping(node, path, problems, out var mapping))
        {
            return false;
        }

        var fields = new MappingFields(mapping, path, problems);
        var pool = fields.Optional<long>("candidate_pool", Constraints.IntAtLeast(1), 3);
        var temperature = fields.Optional<double>("temperature", Constraints.FloatAbove(0.0), 0.15);
        var recovery = fields.Optional<double>("saturation_recovery", Constraints.FloatAtLeast(0.0), 0.004);
        var novelty = fields.Optional<double>("novelty_weight", Constraints.FloatIn(0.0, 1.0), 0.6);
        var lossAversion = fields.Optional<double>("loss_aversion", Constraints.FloatAtLeast(0.0), 1.0);
        var learningRate = fields.Optional<double>("learning_rate", Constraints.FloatIn(0.0, 1.0), 0.0);
        var strain = fields.Optional("strain_multipliers",
            CollectionReader.Map<TemperamentKnob, double>(FieldReader.TryTemperamentKnob, Constraints.FloatAbove(0.0)), DeclaredMap<TemperamentKnob, double>.Empty);
        var duration = fields.Optional<double>("duration_discount", Constraints.FloatInHalfOpen(0.0, 1.0), 1.0);
        var switchCost = fields.Optional<double>("switch_cost", Constraints.FloatIn(0.0, 1.0), 0.0);
        var criticalNeed = fields.Optional<double>("critical_need_threshold", Constraints.FloatIn(0.0, 1.0), 0.15);
        var intentConfidence = fields.Optional<double>("intent_confidence_threshold", Constraints.FloatIn(0.0, 1.0), 0.35);
        fields.RejectExtras();
        if (problems.Count != before)
        {
            return false;
        }

        value = new TuningSpec(pool, temperature, recovery, novelty, lossAversion, learningRate, strain, duration,
            switchCost, criticalNeed, intentConfidence);
        return true;
    }

    // frozenset de la 0.1: sin repetidos, y en orden de punto de código al volcarlo.
    private static NodeReader<ImmutableArray<T>> SortedUnique<T>(NodeReader<T> item, Func<T, string> key) where T : notnull =>
        (YamlNode node, string path, ProblemList problems, out ImmutableArray<T> value) =>
        {
            if (!CollectionReader.Items(item)(node, path, problems, out var items))
            {
                value = ImmutableArray<T>.Empty;
                return false;
            }

            value = items.Distinct().OrderBy(key, CodePointComparer.Instance).ToImmutableArray();
            return true;
        };
}
