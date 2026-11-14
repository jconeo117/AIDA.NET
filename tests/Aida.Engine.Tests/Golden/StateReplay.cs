using System.Text.Json;
using Aida.Engine.Events;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;
using Aida.Engine.State;

namespace Aida.Engine.Tests.Golden;

internal static class StateReplay
{
    private static readonly Dictionary<string, ContentPack> PackCache = new(StringComparer.Ordinal);

    internal static ContentPack GetPack(string packName)
    {
        if (PackCache.TryGetValue(packName, out var pack))
        {
            return pack;
        }

        var path = packName switch
        {
            "plantilla" => Path.Combine(GoldenFiles.RepoRoot, "packs", "_plantilla", "pack.yaml"),
            "consultorio" => Path.Combine(GoldenFiles.RepoRoot, "packs", "health", "consultorio", "pack.yaml"),
            _ => Path.Combine(GoldenFiles.GoldenDir, "state", "packs", $"{packName}.yaml"),
        };

        pack = PackLoader.Load(path);
        PackCache[packName] = pack;
        return pack;
    }

    internal static void ExecuteOp(AgentState agent, ContentPack pack, EventLog log, JsonElement op, string opType)
    {
        switch (opType)
        {
            case "tick":
                var tick = op.GetProperty("tick").GetInt64();
                agent.Decay(pack);
                agent.AdvanceFloorStreak(pack, log, tick);
                agent.RecoverNovelty(pack.Tuning.SaturationRecovery);
                agent.RefreshState(pack);
                agent.Recompose(pack, []);
                break;
            case "decay":
                agent.Decay(pack);
                break;
            case "floor":
                var fTick = op.GetProperty("tick").GetInt64();
                agent.AdvanceFloorStreak(pack, log, fTick);
                break;
            case "recover":
                agent.RecoverNovelty(pack.Tuning.SaturationRecovery);
                break;
            case "refresh":
                agent.RefreshState(pack);
                break;
            case "recompose":
                var situations = op.GetProperty("situations").EnumerateArray()
                    .Select(s => new SituationId(s.GetString()!))
                    .ToList();
                agent.Recompose(pack, situations);
                break;
            case "apply":
                var applyAction = pack.Actions.First(a => a.Id.Value == op.GetProperty("action").GetString()!);
                agent.Apply(applyAction.Effects, pack);
                break;
            case "saturate":
                var satAction = pack.Actions.First(a => a.Id.Value == op.GetProperty("action").GetString()!);
                agent.Saturate(satAction.Id, satAction.SaturationGain);
                break;
            case "begin":
                var beginAction = new ActionId(op.GetProperty("action").GetString()!);
                var ticksRemaining = op.GetProperty("ticks").GetInt64();
                var totalTicks = op.GetProperty("total").GetInt64();
                agent.Performing = new PerformingAction(beginAction, ticksRemaining, totalTicks);
                break;
            case "advance":
                agent.Advance();
                break;
            case "idle":
                agent.Performing = null;
                break;
            default:
                throw new InvalidOperationException($"Operación desconocida: {opType}");
        }
    }
}
