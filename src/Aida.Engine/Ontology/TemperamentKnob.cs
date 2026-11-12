namespace Aida.Engine.Ontology;

/// <summary>Una perilla del temperamento: cómo decide un agente, no qué quiere.</summary>
/// <remarks>
/// Los miembros siguen el orden de la 0.1 (<c>engine/ontology.py:250-307</c>). El nombre en el YAML lo da
/// <see cref="OntologyNames"/>; el valor numérico no se escribe nunca.
/// </remarks>
public enum TemperamentKnob
{
    /// <summary>Cuánto se aleja la decisión de la mejor opción.</summary>
    Temperature,

    /// <summary>Cuánto pesa perder frente a ganar.</summary>
    LossAversion,

    /// <summary>Por debajo de qué nivel una necesidad es una crisis.</summary>
    CriticalThreshold,

    /// <summary>Cuánto cuesta cambiar de una cosa a otra.</summary>
    SwitchCost,

    /// <summary>Cuánto pesa lo nuevo frente a lo conocido.</summary>
    Novelty,

    /// <summary>Con qué velocidad se aprende de lo que pasa.</summary>
    LearningRate,
}
