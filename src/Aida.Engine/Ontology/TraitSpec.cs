namespace Aida.Engine.Ontology;

/// <summary>Un rasgo de personalidad: qué hace un rasgo, compartido por todo el catálogo del pack.</summary>
/// <remarks>
/// Origen: <c>engine/ontology.py:310-425</c>. Cada multiplicador se compone sobre el valor del eje, nunca lo
/// sustituye. Los rangos y las claves los comprueba el cargador.
/// </remarks>
public sealed class TraitSpec
{
    internal TraitSpec(TraitId id, string label, DeclaredMap<NeedId, double> needWeightMultipliers,
        DeclaredMap<NeedId, double> gainMultipliers, DeclaredMap<NeedId, double> costMultipliers,
        DeclaredMap<NeedId, double> capMultipliers, long phaseShift,
        DeclaredMap<TemperamentKnob, double> temperamentMultipliers)
    {
        Id = id;
        Label = label;
        NeedWeightMultipliers = needWeightMultipliers;
        GainMultipliers = gainMultipliers;
        CostMultipliers = costMultipliers;
        CapMultipliers = capMultipliers;
        PhaseShift = phaseShift;
        TemperamentMultipliers = temperamentMultipliers;
    }

    /// <summary>El identificador del rasgo.</summary>
    public TraitId Id { get; }

    /// <summary>Nombre legible, solo para logs.</summary>
    public string Label { get; }

    /// <summary>Multiplicador del peso de cada necesidad.</summary>
    public DeclaredMap<NeedId, double> NeedWeightMultipliers { get; }

    /// <summary>Multiplicador de lo que rinde cada necesidad al atenderla.</summary>
    public DeclaredMap<NeedId, double> GainMultipliers { get; }

    /// <summary>Multiplicador del coste de cada necesidad.</summary>
    public DeclaredMap<NeedId, double> CostMultipliers { get; }

    /// <summary>Multiplicador del techo de cada necesidad.</summary>
    public DeclaredMap<NeedId, double> CapMultipliers { get; }

    /// <summary>Desfase en ticks, respecto al ciclo, de las franjas que este rasgo afecta.</summary>
    public long PhaseShift { get; }

    /// <summary>Multiplicador de cada perilla del temperamento.</summary>
    public DeclaredMap<TemperamentKnob, double> TemperamentMultipliers { get; }
}
