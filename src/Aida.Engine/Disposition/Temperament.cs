namespace Aida.Engine.Disposition;

/// <summary>Los parámetros de decisión ya compuestos para un agente.</summary>
/// <remarks>
/// Lo construye solo <c>TemperamentComposition</c> con datos que el motor acaba de calcular, y lo leen la deliberación
/// (N005) y la explicación (N007). Es inmutable: un parámetro cambia recomponiendo, nunca editándolo. Origen:
/// <c>engine/disposition.py:261-282</c>.
/// </remarks>
public sealed class Temperament
{
    internal Temperament(double temperature, double lossAversion, double criticalNeedThreshold, double switchCost,
        double noveltyWeight, double learningRate)
    {
        Temperature = temperature;
        LossAversion = lossAversion;
        CriticalNeedThreshold = criticalNeedThreshold;
        SwitchCost = switchCost;
        NoveltyWeight = noveltyWeight;
        LearningRate = learningRate;
    }

    /// <summary>La temperatura de la elección entre candidatas. Nunca menor que <c>1e-6</c>, sin techo superior.</summary>
    public double Temperature { get; }

    /// <summary>Cuánto pesa perder frente a ganar. Sin techo: un agente paralizado ante el coste es un arquetipo válido (<c>disposition.py:305-307</c>).</summary>
    public double LossAversion { get; }

    /// <summary>Por debajo de qué nivel una necesidad cuenta como crisis. Acotado a 1.0.</summary>
    public double CriticalNeedThreshold { get; }

    /// <summary>Lo que cuesta cambiar de una tarea a otra. Acotado a 1.0.</summary>
    public double SwitchCost { get; }

    /// <summary>El peso de la búsqueda de variedad. Acotado a 1.0.</summary>
    public double NoveltyWeight { get; }

    /// <summary>La tasa de aprendizaje de la regla delta. Acotada a 1.0.</summary>
    public double LearningRate { get; }
}
