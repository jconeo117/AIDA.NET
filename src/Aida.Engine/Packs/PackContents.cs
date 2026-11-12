using System.Collections.Immutable;
using Aida.Engine.Ontology;

namespace Aida.Engine.Packs;

/// <summary>Todo lo que declara un pack, leído y sin comprobar las referencias cruzadas.</summary>
/// <param name="Id">El identificador del pack.</param>
/// <param name="Label">El nombre legible del pack.</param>
/// <param name="Needs">Las necesidades, en el orden del YAML.</param>
/// <param name="Actions">Las acciones, en el orden del YAML.</param>
/// <param name="Agents">Los agentes, en el orden del YAML.</param>
/// <param name="Traits">Los rasgos, en el orden del YAML.</param>
/// <param name="Phases">Las franjas, en el orden del YAML.</param>
/// <param name="Situations">Las situaciones, en el orden del YAML.</param>
/// <param name="Goals">Las metas del pack, en el orden del YAML.</param>
/// <param name="Intents">Las intenciones, en el orden del YAML.</param>
/// <param name="Authority">La autoridad.</param>
/// <param name="Health">La salud.</param>
/// <param name="Tuning">Los ajustes finos.</param>
/// <remarks>
/// Es el paso entre la lectura y las comprobaciones: el orquestador de las 15 trabaja sobre estas listas crudas, sin
/// índices, porque un índice colapsaría los identificadores repetidos que hay que detectar (<c>engine/pack.py:80-83</c>).
/// </remarks>
internal sealed record PackContents(
    string Id,
    string Label,
    ImmutableArray<NeedSpec> Needs,
    ImmutableArray<ActionSpec> Actions,
    ImmutableArray<AgentSpec> Agents,
    ImmutableArray<TraitSpec> Traits,
    ImmutableArray<PhaseSpec> Phases,
    ImmutableArray<SituationSpec> Situations,
    ImmutableArray<GoalSpec> Goals,
    ImmutableArray<IntentSpec> Intents,
    AuthoritySpec Authority,
    HealthSpec Health,
    TuningSpec Tuning);
