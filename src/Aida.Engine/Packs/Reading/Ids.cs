using Aida.Engine.Ontology;

namespace Aida.Engine.Packs.Reading;

/// <summary>Los conversores de los identificadores del pack: un texto, envuelto en su tipo.</summary>
/// <remarks>Sirven tanto para valores como para las claves de los mapas, donde un identificador es siempre texto.</remarks>
internal static class Ids
{
    /// <summary>Lee un identificador de necesidad.</summary>
    internal static NodeReader<NeedId> Need => FieldReader.Id(text => new NeedId(text));

    /// <summary>Lee un identificador de rasgo.</summary>
    internal static NodeReader<TraitId> Trait => FieldReader.Id(text => new TraitId(text));

    /// <summary>Lee un identificador de meta.</summary>
    internal static NodeReader<GoalId> Goal => FieldReader.Id(text => new GoalId(text));

    /// <summary>Lee un identificador de agente.</summary>
    internal static NodeReader<AgentId> Agent => FieldReader.Id(text => new AgentId(text));

    /// <summary>Lee un identificador de franja.</summary>
    internal static NodeReader<PhaseId> Phase => FieldReader.Id(text => new PhaseId(text));

    /// <summary>Lee un identificador de situación.</summary>
    internal static NodeReader<SituationId> Situation => FieldReader.Id(text => new SituationId(text));

    /// <summary>Lee un identificador de intención.</summary>
    internal static NodeReader<IntentId> Intent => FieldReader.Id(text => new IntentId(text));

    /// <summary>Lee un identificador de acción.</summary>
    internal static NodeReader<ActionId> Action => FieldReader.Id(text => new ActionId(text));

    /// <summary>Lee una etiqueta.</summary>
    internal static NodeReader<Tag> TagLabel => FieldReader.Id(text => new Tag(text));
}
