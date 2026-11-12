using System.Collections.Immutable;

namespace Aida.Engine.Ontology;

/// <summary>Quién es un agente: sus metas, sus rasgos, sus gustos y sus necesidades iniciales.</summary>
/// <remarks>
/// Origen: <c>engine/ontology.py:592-639</c>. Reducido a propósito: el motor modela un único agente por ahora.
/// Un rasgo ausente de <see cref="Traits"/> no es un 0.0 explícito: no participa en la composición.
/// </remarks>
public sealed class AgentSpec
{
    internal AgentSpec(AgentId id, string label, ImmutableArray<GoalSpec> goals,
        DeclaredMap<TraitId, double> traits, ImmutableArray<TasteSpec> tastes,
        DeclaredMap<NeedId, double> initialNeeds)
    {
        Id = id;
        Label = label;
        Goals = goals;
        Traits = traits;
        Tastes = tastes;
        InitialNeeds = initialNeeds;
    }

    /// <summary>El identificador del agente.</summary>
    public AgentId Id { get; }

    /// <summary>Nombre legible, solo para logs.</summary>
    public string Label { get; }

    /// <summary>Lo que el agente quiere, en el orden del YAML. Vacío es legítimo.</summary>
    public ImmutableArray<GoalSpec> Goals { get; }

    /// <summary>La intensidad de cada rasgo de este agente.</summary>
    public DeclaredMap<TraitId, double> Traits { get; }

    /// <summary>Los gustos de este agente, en el orden del YAML.</summary>
    public ImmutableArray<TasteSpec> Tastes { get; }

    /// <summary>El valor inicial de cada necesidad, cuando difiere del declarado en la necesidad.</summary>
    public DeclaredMap<NeedId, double> InitialNeeds { get; }
}
