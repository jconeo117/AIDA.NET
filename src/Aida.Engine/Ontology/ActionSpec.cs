using System.Collections.Immutable;

namespace Aida.Engine.Ontology;

/// <summary>Una entrada del catálogo cerrado de acciones: latencia, requisitos, impacto anticipado y efecto real.</summary>
/// <remarks>
/// Origen: <c>engine/ontology.py:836-927</c>. <see cref="Satisfies"/> se usa al deliberar y <see cref="Effects"/>
/// al completar: separarlos deja modelar el error de predicción. Las tres comprobaciones de signos y cotas
/// las hace el cargador.
/// </remarks>
public sealed class ActionSpec
{
    internal ActionSpec(ActionId id, string label, Reversibility reversibility, long durationTicks, bool interruptible,
        DeclaredMap<NeedId, double> requires, DeclaredMap<TraitId, double> requiresTraits,
        DeclaredMap<NeedId, double> satisfies, DeclaredMap<NeedId, double> costs, DeclaredMap<NeedId, double> effects,
        DeclaredMap<PhaseId, double> phaseMultipliers, DeclaredMap<SituationId, double> situationMultipliers,
        double saturationGain, ImmutableArray<Tag> tags)
    {
        Id = id;
        Label = label;
        Reversibility = reversibility;
        DurationTicks = durationTicks;
        Interruptible = interruptible;
        Requires = requires;
        RequiresTraits = requiresTraits;
        Satisfies = satisfies;
        Costs = costs;
        Effects = effects;
        PhaseMultipliers = phaseMultipliers;
        SituationMultipliers = situationMultipliers;
        SaturationGain = saturationGain;
        Tags = tags;
    }

    /// <summary>El identificador de la acción.</summary>
    public ActionId Id { get; }

    /// <summary>Nombre legible, solo para logs.</summary>
    public string Label { get; }

    /// <summary>Si lo que hace se puede deshacer. Obligatorio: no tiene valor por defecto.</summary>
    public Reversibility Reversibility { get; }

    /// <summary>Latencia esperada, en ticks.</summary>
    public long DurationTicks { get; }

    /// <summary>Si una urgencia crítica puede abortarla a medias.</summary>
    public bool Interruptible { get; }

    /// <summary>Nivel mínimo por eje para poder intentarla.</summary>
    public DeclaredMap<NeedId, double> Requires { get; }

    /// <summary>Intensidad mínima de rasgo requerida, en [-1, 1].</summary>
    public DeclaredMap<TraitId, double> RequiresTraits { get; }

    /// <summary>Cuánto anticipa aliviar cada necesidad; se evalúa al deliberar.</summary>
    public DeclaredMap<NeedId, double> Satisfies { get; }

    /// <summary>Cuánto anticipa gastar en cada necesidad; se evalúa al deliberar.</summary>
    public DeclaredMap<NeedId, double> Costs { get; }

    /// <summary>El delta real aplicado al estado al completar la acción.</summary>
    public DeclaredMap<NeedId, double> Effects { get; }

    /// <summary>Multiplicador de beneficio según el tramo del ciclo.</summary>
    public DeclaredMap<PhaseId, double> PhaseMultipliers { get; }

    /// <summary>Multiplicador de beneficio según la situación activa.</summary>
    public DeclaredMap<SituationId, double> SituationMultipliers { get; }

    /// <summary>Hastío acumulado por repetición continua de esta acción.</summary>
    public double SaturationGain { get; }

    /// <summary>Etiquetas libres, ordenadas por punto de código y sin repetidos.</summary>
    public ImmutableArray<Tag> Tags { get; }
}
