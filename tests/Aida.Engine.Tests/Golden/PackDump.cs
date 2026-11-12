using System.Collections.Immutable;
using System.Text;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;
using Aida.Engine.Serialization;

namespace Aida.Engine.Tests.Golden;

/// <summary>Escribe un <see cref="ContentPack"/> con el volcado canónico de la 0.1 de Python.</summary>
internal static class PackDump
{
    internal static string Dump(ContentPack pack)
    {
        var sb = new StringBuilder();
        WritePack(sb, pack);
        return sb.ToString();
    }

    private static void WritePack(StringBuilder sb, ContentPack p)
    {
        sb.Append('{');
        WriteKey(sb, "actions");
        WriteArray(sb, p.Actions, WriteAction);
        sb.Append(',');
        WriteKey(sb, "agents");
        WriteArray(sb, p.Agents, WriteAgent);
        sb.Append(',');
        WriteKey(sb, "authority");
        WriteAuthority(sb, p.Authority);
        sb.Append(',');
        WriteKey(sb, "goals");
        WriteArray(sb, p.Goals, WriteGoal);
        sb.Append(',');
        WriteKey(sb, "health");
        WriteHealth(sb, p.Health);
        sb.Append(',');
        WriteKey(sb, "id");
        CanonicalJson.WriteString(sb, p.Id);
        sb.Append(',');
        WriteKey(sb, "intents");
        WriteArray(sb, p.Intents, WriteIntent);
        sb.Append(',');
        WriteKey(sb, "label");
        CanonicalJson.WriteString(sb, p.Label);
        sb.Append(',');
        WriteKey(sb, "needs");
        WriteArray(sb, p.Needs, WriteNeed);
        sb.Append(',');
        WriteKey(sb, "phases");
        WriteArray(sb, p.Phases, WritePhase);
        sb.Append(',');
        WriteKey(sb, "situations");
        WriteArray(sb, p.Situations, WriteSituation);
        sb.Append(',');
        WriteKey(sb, "traits");
        WriteArray(sb, p.Traits, WriteTrait);
        sb.Append(',');
        WriteKey(sb, "tuning");
        WriteTuning(sb, p.Tuning);
        sb.Append('}');
    }

    private static void WriteNeed(StringBuilder sb, NeedSpec n)
    {
        sb.Append('{');
        WriteKey(sb, "base_weight");
        sb.Append(CanonicalJson.FormatDouble(n.BaseWeight));
        sb.Append(',');
        WriteKey(sb, "cap");
        sb.Append(CanonicalJson.FormatDouble(n.Cap));
        sb.Append(',');
        WriteKey(sb, "decay_per_tick");
        sb.Append(CanonicalJson.FormatDouble(n.DecayPerTick));
        sb.Append(',');
        WriteKey(sb, "escalates_after");
        sb.Append(CanonicalJson.FormatInteger(n.EscalatesAfter));
        sb.Append(',');
        WriteKey(sb, "id");
        CanonicalJson.WriteString(sb, n.Id.Value);
        sb.Append(',');
        WriteKey(sb, "initial");
        sb.Append(CanonicalJson.FormatDouble(n.Initial));
        sb.Append(',');
        WriteKey(sb, "label");
        CanonicalJson.WriteString(sb, n.Label);
        sb.Append(',');
        WriteKey(sb, "max_debt");
        sb.Append(CanonicalJson.FormatDouble(n.MaxDebt));
        sb.Append(',');
        WriteKey(sb, "progression");
        sb.Append(n.Progression ? "true" : "false");
        sb.Append(',');
        WriteKey(sb, "strain_weight");
        sb.Append(CanonicalJson.FormatDouble(n.StrainWeight));
        sb.Append(',');
        WriteKey(sb, "urgency_exponent");
        sb.Append(CanonicalJson.FormatDouble(n.UrgencyExponent));
        sb.Append(',');
        WriteKey(sb, "urgent_when_low");
        sb.Append(n.UrgentWhenLow ? "true" : "false");
        sb.Append('}');
    }

    private static void WriteTrait(StringBuilder sb, TraitSpec t)
    {
        sb.Append('{');
        WriteKey(sb, "cap_multipliers");
        WriteDoubleMap(sb, t.CapMultipliers, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "cost_multipliers");
        WriteDoubleMap(sb, t.CostMultipliers, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "gain_multipliers");
        WriteDoubleMap(sb, t.GainMultipliers, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "id");
        CanonicalJson.WriteString(sb, t.Id.Value);
        sb.Append(',');
        WriteKey(sb, "label");
        CanonicalJson.WriteString(sb, t.Label);
        sb.Append(',');
        WriteKey(sb, "need_weight_multipliers");
        WriteDoubleMap(sb, t.NeedWeightMultipliers, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "phase_shift");
        sb.Append(CanonicalJson.FormatInteger(t.PhaseShift));
        sb.Append(',');
        WriteKey(sb, "temperament_multipliers");
        WriteDoubleMap(sb, t.TemperamentMultipliers, OntologyNames.ToWire);
        sb.Append('}');
    }

    private static void WriteTaste(StringBuilder sb, TasteSpec t)
    {
        sb.Append('{');
        WriteKey(sb, "multiplier");
        sb.Append(CanonicalJson.FormatDouble(t.Multiplier));
        sb.Append(',');
        WriteKey(sb, "target");
        CanonicalJson.WriteString(sb, t.Target);
        sb.Append('}');
    }

    private static void WriteGoal(StringBuilder sb, GoalSpec g)
    {
        sb.Append('{');
        WriteKey(sb, "at_least");
        WriteNullableDouble(sb, g.AtLeast);
        sb.Append(',');
        WriteKey(sb, "at_traits");
        WriteDoubleMap(sb, g.AtTraits, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "deadline");
        WriteNullableInteger(sb, g.Deadline);
        sb.Append(',');
        WriteKey(sb, "id");
        CanonicalJson.WriteString(sb, g.Id.Value);
        sb.Append(',');
        WriteKey(sb, "label");
        CanonicalJson.WriteString(sb, g.Label);
        sb.Append(',');
        WriteKey(sb, "need");
        CanonicalJson.WriteString(sb, g.Need.Value);
        sb.Append(',');
        WriteKey(sb, "on_failure");
        WriteDoubleMap(sb, g.OnFailure, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "on_success");
        WriteDoubleMap(sb, g.OnSuccess, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "requires_goal");
        WriteNullableString(sb, g.RequiresGoal?.Value);
        sb.Append(',');
        WriteKey(sb, "weight_multiplier");
        sb.Append(CanonicalJson.FormatDouble(g.WeightMultiplier));
        sb.Append('}');
    }

    private static void WriteAgent(StringBuilder sb, AgentSpec a)
    {
        sb.Append('{');
        WriteKey(sb, "goals");
        WriteArray(sb, a.Goals, WriteGoal);
        sb.Append(',');
        WriteKey(sb, "id");
        CanonicalJson.WriteString(sb, a.Id.Value);
        sb.Append(',');
        WriteKey(sb, "initial_needs");
        WriteDoubleMap(sb, a.InitialNeeds, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "label");
        CanonicalJson.WriteString(sb, a.Label);
        sb.Append(',');
        WriteKey(sb, "tastes");
        WriteArray(sb, a.Tastes, WriteTaste);
        sb.Append(',');
        WriteKey(sb, "traits");
        WriteDoubleMap(sb, a.Traits, k => k.Value);
        sb.Append('}');
    }

    private static void WriteIntent(StringBuilder sb, IntentSpec i)
    {
        sb.Append('{');
        WriteKey(sb, "examples");
        WriteArray(sb, i.Examples, CanonicalJson.WriteString);
        sb.Append(',');
        WriteKey(sb, "id");
        CanonicalJson.WriteString(sb, i.Id.Value);
        sb.Append(',');
        WriteKey(sb, "label");
        CanonicalJson.WriteString(sb, i.Label);
        sb.Append(',');
        WriteKey(sb, "need");
        CanonicalJson.WriteString(sb, i.Need.Value);
        sb.Append('}');
    }

    private static void WritePhase(StringBuilder sb, PhaseSpec p)
    {
        sb.Append('{');
        WriteKey(sb, "id");
        CanonicalJson.WriteString(sb, p.Id.Value);
        sb.Append(',');
        WriteKey(sb, "label");
        CanonicalJson.WriteString(sb, p.Label);
        sb.Append(',');
        WriteKey(sb, "starts_at");
        sb.Append(CanonicalJson.FormatInteger(p.StartsAt));
        sb.Append('}');
    }

    private static void WriteSituation(StringBuilder sb, SituationSpec s)
    {
        sb.Append('{');
        WriteKey(sb, "effects");
        WriteDoubleMap(sb, s.Effects, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "every");
        sb.Append(CanonicalJson.FormatInteger(s.Every));
        sb.Append(',');
        WriteKey(sb, "gain_multipliers");
        WriteDoubleMap(sb, s.GainMultipliers, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "id");
        CanonicalJson.WriteString(sb, s.Id.Value);
        sb.Append(',');
        WriteKey(sb, "label");
        CanonicalJson.WriteString(sb, s.Label);
        sb.Append(',');
        WriteKey(sb, "lasts");
        sb.Append(CanonicalJson.FormatInteger(s.Lasts));
        sb.Append(',');
        WriteKey(sb, "starts_at");
        sb.Append(CanonicalJson.FormatInteger(s.StartsAt));
        sb.Append(',');
        WriteKey(sb, "temperament_multipliers");
        WriteDoubleMap(sb, s.TemperamentMultipliers, OntologyNames.ToWire);
        sb.Append('}');
    }

    private static void WriteAction(StringBuilder sb, ActionSpec a)
    {
        sb.Append('{');
        WriteKey(sb, "costs");
        WriteDoubleMap(sb, a.Costs, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "duration_ticks");
        sb.Append(CanonicalJson.FormatInteger(a.DurationTicks));
        sb.Append(',');
        WriteKey(sb, "effects");
        WriteDoubleMap(sb, a.Effects, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "id");
        CanonicalJson.WriteString(sb, a.Id.Value);
        sb.Append(',');
        WriteKey(sb, "interruptible");
        sb.Append(a.Interruptible ? "true" : "false");
        sb.Append(',');
        WriteKey(sb, "label");
        CanonicalJson.WriteString(sb, a.Label);
        sb.Append(',');
        WriteKey(sb, "phase_multipliers");
        WriteDoubleMap(sb, a.PhaseMultipliers, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "requires");
        WriteDoubleMap(sb, a.Requires, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "requires_traits");
        WriteDoubleMap(sb, a.RequiresTraits, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "reversibility");
        CanonicalJson.WriteString(sb, OntologyNames.ToWire(a.Reversibility));
        sb.Append(',');
        WriteKey(sb, "satisfies");
        WriteDoubleMap(sb, a.Satisfies, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "saturation_gain");
        sb.Append(CanonicalJson.FormatDouble(a.SaturationGain));
        sb.Append(',');
        WriteKey(sb, "situation_multipliers");
        WriteDoubleMap(sb, a.SituationMultipliers, k => k.Value);
        sb.Append(',');
        WriteKey(sb, "tags");
        WriteSortedStrings(sb, a.Tags.Select(t => t.Value));
        sb.Append('}');
    }

    private static void WriteAuthority(StringBuilder sb, AuthoritySpec a)
    {
        sb.Append('{');
        WriteKey(sb, "autonomous_up_to");
        CanonicalJson.WriteString(sb, OntologyNames.ToWire(a.AutonomousUpTo));
        sb.Append(',');
        WriteKey(sb, "confirmation_ttl_ticks");
        sb.Append(CanonicalJson.FormatInteger(a.ConfirmationTtlTicks));
        sb.Append('}');
    }

    private static void WriteHealth(StringBuilder sb, HealthSpec h)
    {
        sb.Append('{');
        WriteKey(sb, "days");
        sb.Append(CanonicalJson.FormatInteger(h.Days));
        sb.Append(',');
        WriteKey(sb, "max_abort_ratio");
        WriteNullableDouble(sb, h.MaxAbortRatio);
        sb.Append(',');
        WriteKey(sb, "max_crisis");
        WriteNullableDouble(sb, h.MaxCrisis);
        sb.Append(',');
        WriteKey(sb, "may_go_unused");
        WriteSortedStrings(sb, h.MayGoUnused.Select(id => id.Value));
        sb.Append(',');
        WriteKey(sb, "min_crisis");
        WriteNullableDouble(sb, h.MinCrisis);
        sb.Append(',');
        WriteKey(sb, "min_differentiation");
        WriteNullableDouble(sb, h.MinDifferentiation);
        sb.Append(',');
        WriteKey(sb, "seeds");
        WriteArray(sb, h.Seeds, (builder, seed) => builder.Append(CanonicalJson.FormatInteger(seed)));
        sb.Append('}');
    }

    private static void WriteTuning(StringBuilder sb, TuningSpec t)
    {
        sb.Append('{');
        WriteKey(sb, "candidate_pool");
        sb.Append(CanonicalJson.FormatInteger(t.CandidatePool));
        sb.Append(',');
        WriteKey(sb, "critical_need_threshold");
        sb.Append(CanonicalJson.FormatDouble(t.CriticalNeedThreshold));
        sb.Append(',');
        WriteKey(sb, "duration_discount");
        sb.Append(CanonicalJson.FormatDouble(t.DurationDiscount));
        sb.Append(',');
        WriteKey(sb, "intent_confidence_threshold");
        sb.Append(CanonicalJson.FormatDouble(t.IntentConfidenceThreshold));
        sb.Append(',');
        WriteKey(sb, "learning_rate");
        sb.Append(CanonicalJson.FormatDouble(t.LearningRate));
        sb.Append(',');
        WriteKey(sb, "loss_aversion");
        sb.Append(CanonicalJson.FormatDouble(t.LossAversion));
        sb.Append(',');
        WriteKey(sb, "novelty_weight");
        sb.Append(CanonicalJson.FormatDouble(t.NoveltyWeight));
        sb.Append(',');
        WriteKey(sb, "saturation_recovery");
        sb.Append(CanonicalJson.FormatDouble(t.SaturationRecovery));
        sb.Append(',');
        WriteKey(sb, "strain_multipliers");
        WriteDoubleMap(sb, t.StrainMultipliers, OntologyNames.ToWire);
        sb.Append(',');
        WriteKey(sb, "switch_cost");
        sb.Append(CanonicalJson.FormatDouble(t.SwitchCost));
        sb.Append(',');
        WriteKey(sb, "temperature");
        sb.Append(CanonicalJson.FormatDouble(t.Temperature));
        sb.Append('}');
    }

    private static void WriteKey(StringBuilder sb, string key)
    {
        CanonicalJson.WriteString(sb, key);
        sb.Append(':');
    }

    private static void WriteArray<T>(StringBuilder sb, ImmutableArray<T> items, Action<StringBuilder, T> writeItem)
    {
        sb.Append('[');
        for (var i = 0; i < items.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            writeItem(sb, items[i]);
        }

        sb.Append(']');
    }

    private static void WriteSortedStrings(StringBuilder sb, IEnumerable<string> items)
    {
        sb.Append('[');
        var sorted = items.OrderBy(s => s, CodePointComparer.Instance).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            CanonicalJson.WriteString(sb, sorted[i]);
        }

        sb.Append(']');
    }

    private static void WriteDoubleMap<TKey>(StringBuilder sb, IEnumerable<KeyValuePair<TKey, double>> map, Func<TKey, string> keyToString)
    {
        sb.Append('{');
        var sorted = map.Select(p => new KeyValuePair<string, double>(keyToString(p.Key), p.Value))
            .OrderBy(p => p.Key, CodePointComparer.Instance)
            .ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            CanonicalJson.WriteString(sb, sorted[i].Key);
            sb.Append(':');
            sb.Append(CanonicalJson.FormatDouble(sorted[i].Value));
        }

        sb.Append('}');
    }

    private static void WriteNullableDouble(StringBuilder sb, double? value)
    {
        if (value.HasValue)
        {
            sb.Append(CanonicalJson.FormatDouble(value.Value));
        }
        else
        {
            sb.Append("null");
        }
    }

    private static void WriteNullableInteger(StringBuilder sb, long? value)
    {
        if (value.HasValue)
        {
            sb.Append(CanonicalJson.FormatInteger(value.Value));
        }
        else
        {
            sb.Append("null");
        }
    }

    private static void WriteNullableString(StringBuilder sb, string? value)
    {
        if (value is not null)
        {
            CanonicalJson.WriteString(sb, value);
        }
        else
        {
            sb.Append("null");
        }
    }
}
