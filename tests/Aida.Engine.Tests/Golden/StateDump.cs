using System.Text;
using Aida.Engine.Ontology;
using Aida.Engine.Serialization;
using Aida.Engine.State;

namespace Aida.Engine.Tests.Golden;

/// <summary>Escribe un <see cref="AgentState"/> con el volcado canónico de la 0.1 de Python.</summary>
internal static class StateDump
{
    internal static string Dump(AgentState state)
    {
        var sb = new StringBuilder();
        sb.Append('{');

        WriteKey(sb, "agent_id");
        CanonicalJson.WriteString(sb, state.AgentId.Value);
        sb.Append(',');

        WriteKey(sb, "caps");
        WriteDoubleMap(sb, state.Caps, k => k.Value);
        sb.Append(',');

        WriteKey(sb, "costs");
        WriteDoubleMap(sb, state.Costs, k => k.Value);
        sb.Append(',');

        WriteKey(sb, "debt");
        WriteDoubleMap(sb, state.Debt, k => k.Value);
        sb.Append(',');

        WriteKey(sb, "floor_streak");
        WriteLongMap(sb, state.FloorStreak, k => k.Value);
        sb.Append(',');

        WriteKey(sb, "gains");
        WriteDoubleMap(sb, state.Gains, k => k.Value);
        sb.Append(',');

        WriteKey(sb, "goals");
        WriteStringMap(sb, state.Goals, k => k.Value, OntologyNames.ToWire);
        sb.Append(',');

        WriteKey(sb, "need_weights");
        WriteDoubleMap(sb, state.NeedWeights, k => k.Value);
        sb.Append(',');

        WriteKey(sb, "needs");
        WriteDoubleMap(sb, state.Needs, k => k.Value);
        sb.Append(',');

        WriteKey(sb, "performing");
        if (state.Performing is null)
        {
            sb.Append("null");
        }
        else
        {
            sb.Append('{');
            WriteKey(sb, "action");
            CanonicalJson.WriteString(sb, state.Performing.Action.Value);
            sb.Append(',');
            WriteKey(sb, "ticks_remaining");
            sb.Append(CanonicalJson.FormatInteger(state.Performing.TicksRemaining));
            sb.Append(',');
            WriteKey(sb, "total_ticks");
            sb.Append(CanonicalJson.FormatInteger(state.Performing.TotalTicks));
            sb.Append('}');
        }

        sb.Append(',');

        WriteKey(sb, "saturation");
        WriteDoubleMap(sb, state.Saturation, k => k.Value);
        sb.Append(',');

        WriteKey(sb, "strain");
        sb.Append(CanonicalJson.FormatDouble(state.Strain));
        sb.Append(',');

        WriteKey(sb, "tastes");
        WriteDoubleMap(sb, state.Tastes, k => k);
        sb.Append(',');

        WriteKey(sb, "temperament");
        sb.Append('{');
        WriteKey(sb, "critical_need_threshold");
        sb.Append(CanonicalJson.FormatDouble(state.Temperament.CriticalNeedThreshold));
        sb.Append(',');
        WriteKey(sb, "learning_rate");
        sb.Append(CanonicalJson.FormatDouble(state.Temperament.LearningRate));
        sb.Append(',');
        WriteKey(sb, "loss_aversion");
        sb.Append(CanonicalJson.FormatDouble(state.Temperament.LossAversion));
        sb.Append(',');
        WriteKey(sb, "novelty_weight");
        sb.Append(CanonicalJson.FormatDouble(state.Temperament.NoveltyWeight));
        sb.Append(',');
        WriteKey(sb, "switch_cost");
        sb.Append(CanonicalJson.FormatDouble(state.Temperament.SwitchCost));
        sb.Append(',');
        WriteKey(sb, "temperature");
        sb.Append(CanonicalJson.FormatDouble(state.Temperament.Temperature));
        sb.Append('}');
        sb.Append(',');

        WriteKey(sb, "traits");
        WriteDoubleMap(sb, state.Traits, k => k.Value);

        sb.Append('}');
        return sb.ToString();
    }

    private static void WriteKey(StringBuilder sb, string key)
    {
        CanonicalJson.WriteString(sb, key);
        sb.Append(':');
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

    private static void WriteLongMap<TKey>(StringBuilder sb, IEnumerable<KeyValuePair<TKey, long>> map, Func<TKey, string> keyToString)
    {
        sb.Append('{');
        var sorted = map.Select(p => new KeyValuePair<string, long>(keyToString(p.Key), p.Value))
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
            sb.Append(CanonicalJson.FormatInteger(sorted[i].Value));
        }

        sb.Append('}');
    }

    private static void WriteStringMap<TKey, TVal>(StringBuilder sb, IEnumerable<KeyValuePair<TKey, TVal>> map, Func<TKey, string> keyToString, Func<TVal, string> valToString)
    {
        sb.Append('{');
        var sorted = map.Select(p => new KeyValuePair<string, string>(keyToString(p.Key), valToString(p.Value)))
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
            CanonicalJson.WriteString(sb, sorted[i].Value);
        }

        sb.Append('}');
    }
}
