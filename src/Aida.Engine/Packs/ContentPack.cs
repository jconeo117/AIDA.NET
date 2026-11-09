using System.Collections.Frozen;
using System.Collections.Immutable;
using Aida.Engine.Ontology;

namespace Aida.Engine.Packs;

/// <summary>Un pack cargado y válido: todo lo que el motor sabe del mundo de un agente.</summary>
/// <remarks>
/// Solo lo construye <see cref="PackLoader"/>, después de las tres capas de validación: campos, definiciones y las 15
/// comprobaciones cruzadas (<c>engine/pack.py:590-612</c>). Un pack inválido no llega a existir (ADR 0012). Los índices
/// se calculan una vez, al construirlo: son seguros porque el cargador ya rechazó los identificadores repetidos.
/// </remarks>
public sealed class ContentPack
{
    internal ContentPack(PackContents contents)
    {
        Id = contents.Id;
        Label = contents.Label;
        Needs = contents.Needs;
        Actions = contents.Actions;
        Agents = contents.Agents;
        Traits = contents.Traits;
        Phases = contents.Phases;
        Situations = contents.Situations;
        Goals = contents.Goals;
        Intents = contents.Intents;
        Authority = contents.Authority;
        Health = contents.Health;
        Tuning = contents.Tuning;
        NeedById = contents.Needs.ToFrozenDictionary(need => need.Id, need => need);
        TraitById = contents.Traits.ToFrozenDictionary(trait => trait.Id, trait => trait);
        SituationById = contents.Situations.ToFrozenDictionary(situation => situation.Id, situation => situation);
        GoalById = contents.Goals.ToFrozenDictionary(goal => goal.Id, goal => goal);
    }

    /// <summary>El identificador del pack.</summary>
    public string Id { get; }

    /// <summary>El nombre legible del pack.</summary>
    public string Label { get; }

    /// <summary>Las necesidades, en el orden del YAML.</summary>
    public ImmutableArray<NeedSpec> Needs { get; }

    /// <summary>Las acciones, en el orden del YAML.</summary>
    public ImmutableArray<ActionSpec> Actions { get; }

    /// <summary>Los agentes, en el orden del YAML.</summary>
    public ImmutableArray<AgentSpec> Agents { get; }

    /// <summary>Los rasgos, en el orden del YAML.</summary>
    public ImmutableArray<TraitSpec> Traits { get; }

    /// <summary>Las franjas del ciclo, en el orden del YAML.</summary>
    public ImmutableArray<PhaseSpec> Phases { get; }

    /// <summary>Las situaciones, en el orden del YAML.</summary>
    public ImmutableArray<SituationSpec> Situations { get; }

    /// <summary>Las metas del pack, en el orden del YAML.</summary>
    public ImmutableArray<GoalSpec> Goals { get; }

    /// <summary>Las intenciones, en el orden del YAML.</summary>
    public ImmutableArray<IntentSpec> Intents { get; }

    /// <summary>La autoridad del asistente.</summary>
    public AuthoritySpec Authority { get; }

    /// <summary>Lo que el pack quiere que sea el comportamiento del asistente.</summary>
    public HealthSpec Health { get; }

    /// <summary>Los ajustes finos del motor.</summary>
    public TuningSpec Tuning { get; }

    /// <summary>Las necesidades por identificador.</summary>
    public FrozenDictionary<NeedId, NeedSpec> NeedById { get; }

    /// <summary>Los rasgos por identificador.</summary>
    public FrozenDictionary<TraitId, TraitSpec> TraitById { get; }

    /// <summary>Las situaciones por identificador.</summary>
    public FrozenDictionary<SituationId, SituationSpec> SituationById { get; }

    /// <summary>Las metas del pack por identificador.</summary>
    public FrozenDictionary<GoalId, GoalSpec> GoalById { get; }
}
