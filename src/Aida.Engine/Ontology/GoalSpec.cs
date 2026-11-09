namespace Aida.Engine.Ontology;

/// <summary>Una meta: un umbral que el agente quiere cumplir sobre un eje o sobre sus rasgos.</summary>
/// <remarks>
/// Origen: <c>engine/ontology.py:474-589</c>. Exactamente una condición (<c>at_least</c> o <c>at_traits</c>), y
/// las dos comprobaciones de la clase las hace el cargador, en el orden de la 0.1.
/// </remarks>
public sealed class GoalSpec
{
    internal GoalSpec(GoalId id, string label, NeedId need, double? atLeast, DeclaredMap<TraitId, double> atTraits,
        GoalId? requiresGoal, double weightMultiplier, long? deadline,
        DeclaredMap<TraitId, double> onSuccess, DeclaredMap<TraitId, double> onFailure)
    {
        Id = id;
        Label = label;
        Need = need;
        AtLeast = atLeast;
        AtTraits = atTraits;
        RequiresGoal = requiresGoal;
        WeightMultiplier = weightMultiplier;
        Deadline = deadline;
        OnSuccess = onSuccess;
        OnFailure = onFailure;
    }

    /// <summary>El identificador de la meta.</summary>
    public GoalId Id { get; }

    /// <summary>Nombre legible, solo para logs.</summary>
    public string Label { get; }

    /// <summary>El eje que la meta quiere llevar al umbral.</summary>
    public NeedId Need { get; }

    /// <summary>El valor del eje que hay que alcanzar, si la meta se mide por eje; si no, <see langword="null"/>.</summary>
    public double? AtLeast { get; }

    /// <summary>Los rasgos que hay que tener, si la meta se mide por rasgos.</summary>
    public DeclaredMap<TraitId, double> AtTraits { get; }

    /// <summary>La meta que hay que lograr antes de que esta empiece, si la hay.</summary>
    public GoalId? RequiresGoal { get; }

    /// <summary>Cuánto empuja mientras dura, multiplicando el peso que ya tuviera ese eje.</summary>
    public double WeightMultiplier { get; }

    /// <summary>Tick absoluto en que se da por perdida, o <see langword="null"/> si no tiene plazo.</summary>
    public long? Deadline { get; }

    /// <summary>Qué le mueve al agente lograr la meta, rasgo a rasgo.</summary>
    public DeclaredMap<TraitId, double> OnSuccess { get; }

    /// <summary>Qué le mueve al agente perderla, rasgo a rasgo.</summary>
    public DeclaredMap<TraitId, double> OnFailure { get; }
}
