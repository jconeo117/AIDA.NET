using System.Collections.Immutable;
using Aida.Engine.Ontology;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Packs.Reading;

/// <summary>Lee la raíz del pack: sus campos en el orden de §A.4 y las listas de cada definición.</summary>
/// <remarks>
/// El orden de los campos de la raíz es el de la clase: <c>id</c>, <c>label</c>, <c>needs</c>, <c>actions</c>,
/// <c>agents</c>, <c>traits</c>, <c>phases</c>, <c>situations</c>, <c>goals</c>, <c>intents</c>, <c>authority</c>,
/// <c>health</c> y <c>tuning</c>. Un bloque que falta toma su valor por defecto.
/// </remarks>
internal static class PackReader
{
    /// <summary>Lee la raíz de un pack.</summary>
    /// <param name="node">La raíz del documento, que debe ser un mapa.</param>
    /// <param name="problems">Donde se anotan los problemas.</param>
    /// <param name="contents">Lo leído, si la lectura no dio problemas.</param>
    /// <returns><see langword="true"/> si la raíz es válida.</returns>
    internal static bool TryRead(YamlNode node, ProblemList problems, out PackContents contents)
    {
        contents = null!;
        var before = problems.Count;
        if (!FieldReader.TryMapping(node, string.Empty, problems, out var mapping))
        {
            return false;
        }

        var fields = new MappingFields(mapping, string.Empty, problems);
        var id = fields.Required<string>("id", FieldReader.TryText);
        var label = fields.Required<string>("label", FieldReader.TryText);
        var needs = fields.Required<ImmutableArray<NeedSpec>>("needs", CollectionReader.Items<NeedSpec>(NeedReader.TryRead));
        var actions = fields.Required<ImmutableArray<ActionSpec>>("actions", CollectionReader.Items<ActionSpec>(ActionReader.TryRead));
        var agents = fields.Required<ImmutableArray<AgentSpec>>("agents", CollectionReader.Items<AgentSpec>(AgentReader.TryRead));
        var traits = fields.Optional<ImmutableArray<TraitSpec>>("traits", CollectionReader.Items<TraitSpec>(TraitReader.TryRead), ImmutableArray<TraitSpec>.Empty);
        var phases = fields.Optional<ImmutableArray<PhaseSpec>>("phases", CollectionReader.Items<PhaseSpec>(PhaseReader.TryRead), ImmutableArray<PhaseSpec>.Empty);
        var situations = fields.Optional<ImmutableArray<SituationSpec>>("situations", CollectionReader.Items<SituationSpec>(SituationReader.TryRead), ImmutableArray<SituationSpec>.Empty);
        var goals = fields.Optional<ImmutableArray<GoalSpec>>("goals", CollectionReader.Items<GoalSpec>(GoalReader.TryRead), ImmutableArray<GoalSpec>.Empty);
        var intents = fields.Optional<ImmutableArray<IntentSpec>>("intents", CollectionReader.Items<IntentSpec>(IntentReader.TryRead), ImmutableArray<IntentSpec>.Empty);
        var authority = fields.Optional<AuthoritySpec>("authority", SettingsReader.TryReadAuthority, SettingsReader.DefaultAuthority());
        var health = fields.Optional<HealthSpec>("health", SettingsReader.TryReadHealth, SettingsReader.DefaultHealth());
        var tuning = fields.Optional<TuningSpec>("tuning", SettingsReader.TryReadTuning, SettingsReader.DefaultTuning());
        fields.RejectExtras();
        if (problems.Count != before)
        {
            return false;
        }

        contents = new PackContents(id, label, needs, actions, agents, traits, phases, situations, goals, intents,
            authority, health, tuning);
        return true;
    }
}
