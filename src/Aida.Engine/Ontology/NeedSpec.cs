namespace Aida.Engine.Ontology;

/// <summary>Un eje de presión interna que decae con el tiempo. El motor no sabe qué mide.</summary>
/// <remarks>
/// Origen: <c>engine/ontology.py:70-244</c>. Los campos y sus valores por defecto son los de la 0.1; los
/// rangos los comprueba el cargador, no esta clase. Sin <c>can_be_critical</c>: ningún consumidor lo usa en la 0.1.
/// </remarks>
public sealed class NeedSpec
{
    internal NeedSpec(NeedId id, string label, double decayPerTick, bool progression, bool urgentWhenLow,
        double initial, double baseWeight, double strainWeight, long escalatesAfter, double maxDebt,
        double urgencyExponent, double cap)
    {
        Id = id;
        Label = label;
        DecayPerTick = decayPerTick;
        Progression = progression;
        UrgentWhenLow = urgentWhenLow;
        Initial = initial;
        BaseWeight = baseWeight;
        StrainWeight = strainWeight;
        EscalatesAfter = escalatesAfter;
        MaxDebt = maxDebt;
        UrgencyExponent = urgencyExponent;
        Cap = cap;
    }

    /// <summary>El identificador del eje.</summary>
    public NeedId Id { get; }

    /// <summary>Nombre legible, solo para logs.</summary>
    public string Label { get; }

    /// <summary>Cuánto baja por tick. Un 0 describe un eje que solo cambia por lo que se hace.</summary>
    public double DecayPerTick { get; }

    /// <summary>Si el eje solo sube: no es una carencia, es cuánto se ha avanzado.</summary>
    public bool Progression { get; }

    /// <summary>Si escasear esto es una emergencia que justifica soltarlo todo.</summary>
    public bool UrgentWhenLow { get; }

    /// <summary>Valor al empezar, en [0, 1].</summary>
    public double Initial { get; }

    /// <summary>Cuánto importa este eje a un agente sin rasgos que lo modulen.</summary>
    public double BaseWeight { get; }

    /// <summary>Cuánto contribuye este eje al desgaste cognitivo cuando escasea.</summary>
    public double StrainWeight { get; }

    /// <summary>Cuántos ticks seguidos en el suelo hacen falta para que la presión escale. 0 = nunca.</summary>
    public long EscalatesAfter { get; }

    /// <summary>Cuánto puede acumularse por debajo del suelo. 0 lo desactiva.</summary>
    public double MaxDebt { get; }

    /// <summary>Exponente de la curva de urgencia: <c>(1 - valor) ** exponente</c>.</summary>
    public double UrgencyExponent { get; }

    /// <summary>Hasta dónde llega este eje para quien no tiene ventaja. 1.0 = sin techo.</summary>
    public double Cap { get; }
}
