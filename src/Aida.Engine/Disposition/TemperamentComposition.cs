using Aida.Engine.Numerics;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;

namespace Aida.Engine.Disposition;

/// <summary>Compone el temperamento a partir de tres capas multiplicativas: rasgos, desgaste y situaciones
/// (<c>engine/disposition.py:47-49, 285-354</c>).</summary>
/// <remarks>
/// La capa base es el <c>TuningSpec</c>. Un valor que el pack apaga en 0.0 no lo revive ningún rasgo ni situación, porque
/// todo es multiplicación. El orden de los factores es el contrato: rasgos por identificador, desgaste y situaciones
/// activas por identificador, con los duplicados de las situaciones contando cada vez.
/// </remarks>
internal static class TemperamentComposition
{
    // Suelo de temperatura: evita la división por cero en la selección probabilística posterior (disposition.py:47).
    private const double MinTemperature = 1e-6;

    /// <summary>Compone el temperamento de un agente.</summary>
    /// <param name="pack">El pack.</param>
    /// <param name="traits">La intensidad de cada rasgo del agente.</param>
    /// <param name="strain">El desgaste actual, en <c>[0, 1]</c>.</param>
    /// <param name="activeSituations">Las situaciones activas. Pueden repetirse, y cada aparición cuenta.</param>
    /// <returns>El temperamento ya compuesto.</returns>
    internal static Temperament Compose(ContentPack pack, IReadOnlyDictionary<TraitId, double> traits, double strain,
        IEnumerable<SituationId> activeSituations)
    {
        var factors = new Dictionary<TemperamentKnob, double>();
        foreach (var knob in Enum.GetValues<TemperamentKnob>())
        {
            factors[knob] = 1.0;
        }

        foreach (var traitId in traits.Keys.OrderBy(t => t, IdOrder.Instance))
        {
            var trait = pack.TraitById[traitId];
            var intensity = traits[traitId];
            foreach (var (knob, multiplier) in trait.TemperamentMultipliers)
            {
                factors[knob] *= PyFloat.Pow(multiplier, intensity);
            }
        }

        foreach (var (knob, multiplier) in pack.Tuning.StrainMultipliers)
        {
            factors[knob] *= PyFloat.Pow(multiplier, strain);
        }

        foreach (var situationId in activeSituations.OrderBy(s => s, IdOrder.Instance))
        {
            var situation = pack.SituationById[situationId];
            foreach (var (knob, multiplier) in situation.TemperamentMultipliers)
            {
                factors[knob] *= multiplier;
            }
        }

        var tuning = pack.Tuning;
        return new Temperament(
            temperature: PyFloat.Max(MinTemperature, tuning.Temperature * factors[TemperamentKnob.Temperature]),
            lossAversion: tuning.LossAversion * factors[TemperamentKnob.LossAversion],
            criticalNeedThreshold: PyFloat.Min(1.0, tuning.CriticalNeedThreshold * factors[TemperamentKnob.CriticalThreshold]),
            switchCost: PyFloat.Min(1.0, tuning.SwitchCost * factors[TemperamentKnob.SwitchCost]),
            noveltyWeight: PyFloat.Min(1.0, tuning.NoveltyWeight * factors[TemperamentKnob.Novelty]),
            learningRate: PyFloat.Min(1.0, tuning.LearningRate * factors[TemperamentKnob.LearningRate]));
    }
}
