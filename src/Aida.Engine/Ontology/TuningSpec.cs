namespace Aida.Engine.Ontology;

/// <summary>Los ajustes finos del motor que el pack puede declarar.</summary>
/// <remarks>
/// Origen: <c>engine/ontology.py:1031-1138</c>. Los valores por defecto son los de la 0.1; los rangos los comprueba
/// el cargador.
/// </remarks>
public sealed class TuningSpec
{
    internal TuningSpec(long candidatePool, double temperature, double saturationRecovery, double noveltyWeight,
        double lossAversion, double learningRate, DeclaredMap<TemperamentKnob, double> strainMultipliers,
        double durationDiscount, double switchCost, double criticalNeedThreshold, double intentConfidenceThreshold)
    {
        CandidatePool = candidatePool;
        Temperature = temperature;
        SaturationRecovery = saturationRecovery;
        NoveltyWeight = noveltyWeight;
        LossAversion = lossAversion;
        LearningRate = learningRate;
        StrainMultipliers = strainMultipliers;
        DurationDiscount = durationDiscount;
        SwitchCost = switchCost;
        CriticalNeedThreshold = criticalNeedThreshold;
        IntentConfidenceThreshold = intentConfidenceThreshold;
    }

    /// <summary>Cuántas candidatas se consideran a la vez al deliberar.</summary>
    public long CandidatePool { get; }

    /// <summary>La temperatura de la elección entre candidatas.</summary>
    public double Temperature { get; }

    /// <summary>La velocidad con que se recupera el hastío de una acción repetida.</summary>
    public double SaturationRecovery { get; }

    /// <summary>El peso de lo nuevo frente a lo conocido.</summary>
    public double NoveltyWeight { get; }

    /// <summary>Cuánto pesa perder frente a ganar.</summary>
    public double LossAversion { get; }

    /// <summary>La velocidad con que se aprende de lo que pasa.</summary>
    public double LearningRate { get; }

    /// <summary>Multiplicador del desgaste por perilla del temperamento.</summary>
    public DeclaredMap<TemperamentKnob, double> StrainMultipliers { get; }

    /// <summary>Cuánto se descuenta la duración de una acción en la elección.</summary>
    public double DurationDiscount { get; }

    /// <summary>El coste de cambiar de una cosa a otra.</summary>
    public double SwitchCost { get; }

    /// <summary>Por debajo de qué nivel una necesidad cuenta como crisis.</summary>
    public double CriticalNeedThreshold { get; }

    /// <summary>Por encima de qué confianza se reconoce una intención.</summary>
    public double IntentConfidenceThreshold { get; }
}
