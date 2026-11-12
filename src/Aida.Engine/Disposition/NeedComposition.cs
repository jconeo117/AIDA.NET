using Aida.Engine.Numerics;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;

namespace Aida.Engine.Disposition;

/// <summary>Compone los cuatro canales por eje del agente: peso, aptitud, temple y techo (<c>engine/disposition.py:52-174</c>).</summary>
/// <remarks>
/// Cada canal arranca en su valor del pack y multiplica por los rasgos del agente elevados a su intensidad. El orden de
/// los productos es el contrato: los rasgos se recorren por identificador y cada rasgo en el orden de declaración.
/// </remarks>
internal static class NeedComposition
{
    /// <summary>El peso de cada necesidad: su base, por los rasgos y por las metas activas.</summary>
    /// <param name="pack">El pack.</param>
    /// <param name="traits">La intensidad de cada rasgo del agente.</param>
    /// <param name="pursuing">Las metas activas del agente.</param>
    /// <returns>Un peso por cada necesidad del pack, en orden de identificador.</returns>
    internal static SortedDictionary<NeedId, double> ComposeNeedWeights(ContentPack pack,
        IReadOnlyDictionary<TraitId, double> traits, IEnumerable<GoalSpec> pursuing)
    {
        var result = Compose(pack, traits, need => need.BaseWeight, trait => trait.NeedWeightMultipliers);
        foreach (var goal in pursuing.OrderBy(g => g.Id, IdOrder.Instance))
        {
            result[goal.Need] *= goal.WeightMultiplier;
        }

        return result;
    }

    /// <summary>La aptitud del agente al reponer cada necesidad. Arranca en 1.0.</summary>
    /// <param name="pack">El pack.</param>
    /// <param name="traits">La intensidad de cada rasgo del agente.</param>
    /// <returns>Una aptitud por cada necesidad del pack, en orden de identificador.</returns>
    internal static SortedDictionary<NeedId, double> ComposeGains(ContentPack pack, IReadOnlyDictionary<TraitId, double> traits) =>
        Compose(pack, traits, _ => 1.0, trait => trait.GainMultipliers);

    /// <summary>El temple del agente al gastar cada necesidad. Arranca en 1.0.</summary>
    /// <param name="pack">El pack.</param>
    /// <param name="traits">La intensidad de cada rasgo del agente.</param>
    /// <returns>Un temple por cada necesidad del pack, en orden de identificador.</returns>
    internal static SortedDictionary<NeedId, double> ComposeCosts(ContentPack pack, IReadOnlyDictionary<TraitId, double> traits) =>
        Compose(pack, traits, _ => 1.0, trait => trait.CostMultipliers);

    /// <summary>El techo efectivo de cada necesidad: su tope nominal por los rasgos, recortado a 1.0.</summary>
    /// <param name="pack">El pack.</param>
    /// <param name="traits">La intensidad de cada rasgo del agente.</param>
    /// <returns>Un techo por cada necesidad del pack, nunca mayor que 1.0, en orden de identificador.</returns>
    internal static SortedDictionary<NeedId, double> ComposeCaps(ContentPack pack, IReadOnlyDictionary<TraitId, double> traits)
    {
        var result = Compose(pack, traits, need => need.Cap, trait => trait.CapMultipliers);
        foreach (var key in result.Keys.ToList())
        {
            result[key] = PyFloat.Min(1.0, result[key]);
        }

        return result;
    }

    // El arranque, después los rasgos en orden de identificador y, dentro de cada rasgo, en orden de declaración.
    private static SortedDictionary<NeedId, double> Compose(ContentPack pack, IReadOnlyDictionary<TraitId, double> traits,
        Func<NeedSpec, double> start, Func<TraitSpec, DeclaredMap<NeedId, double>> multipliers)
    {
        var result = new SortedDictionary<NeedId, double>(IdOrder.Instance);
        foreach (var need in pack.Needs)
        {
            result[need.Id] = start(need);
        }

        foreach (var traitId in traits.Keys.OrderBy(t => t, IdOrder.Instance))
        {
            var trait = pack.TraitById[traitId];
            var intensity = traits[traitId];
            foreach (var (needId, multiplier) in multipliers(trait))
            {
                result[needId] *= PyFloat.Pow(multiplier, intensity);
            }
        }

        return result;
    }
}
