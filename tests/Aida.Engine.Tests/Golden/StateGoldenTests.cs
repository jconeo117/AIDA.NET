using System.Text;
using System.Text.Json;
using Aida.Engine.Events;
using Aida.Engine.Hashing;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;
using Aida.Engine.State;

namespace Aida.Engine.Tests.Golden;

public sealed class StateGoldenTests
{
    private static readonly Dictionary<string, ContentPack> PackCache = new(StringComparer.Ordinal);

    private static ContentPack GetPack(string packName)
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

    [Fact]
    [Trait("Category", "Golden")]
    public void Los_agentes_nacen_como_en_la_01()
    {
        var lines = GoldenFiles.ReadLines("state/cases.jsonl");
        var diffs = new List<(string CaseName, string Expected, string Actual)>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var caseName = root.GetProperty("name").GetString()!;
            var packName = root.GetProperty("pack").GetString()!;
            var agentId = root.GetProperty("agent").GetString()!;
            var expectedInitial = root.GetProperty("initial").GetString()!;

            var pack = GetPack(packName);
            var agentSpec = pack.Agents.First(a => a.Id.Value == agentId);
            var agent = AgentState.FromSpec(pack, agentSpec);
            var actualInitial = StateDump.Dump(agent);

            if (!string.Equals(actualInitial, expectedInitial, StringComparison.Ordinal))
            {
                diffs.Add((caseName, expectedInitial, actualInitial));
            }
        }

        if (diffs.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Se encontraron {diffs.Count} diferencias en nacimiento de agentes:");
            foreach (var (cName, exp, act) in diffs)
            {
                sb.AppendLine($"[{cName}]");
                sb.AppendLine($"  Esperado: {exp}");
                sb.AppendLine($"  Obtenido: {act}");
            }

            Assert.Fail(sb.ToString());
        }
    }

    [Fact]
    [Trait("Category", "Golden")]
    public void Las_trazas_de_estado_coinciden_con_la_01()
    {
        var lines = GoldenFiles.ReadLines("state/cases.jsonl");

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var caseName = root.GetProperty("name").GetString()!;
            var packName = root.GetProperty("pack").GetString()!;
            var agentId = root.GetProperty("agent").GetString()!;
            var expectedFinal = root.GetProperty("final").GetString()!;
            var expectedSteps = root.GetProperty("steps").EnumerateArray().Select(e => e.GetString()!).ToList();
            var expectedEvents = root.GetProperty("events").EnumerateArray().Select(e => e.GetString()!).ToList();
            var ops = root.GetProperty("ops").EnumerateArray().ToList();

            var pack = GetPack(packName);
            var agentSpec = pack.Agents.First(a => a.Id.Value == agentId);
            var agent = AgentState.FromSpec(pack, agentSpec);
            var log = new EventLog();

            for (var stepIndex = 0; stepIndex < ops.Count; stepIndex++)
            {
                var op = ops[stepIndex];
                var opType = op.GetProperty("op").GetString()!;

                ExecuteOp(agent, pack, log, op, opType);

                var actualDump = StateDump.Dump(agent);
                var actualHash = Convert.ToHexStringLower(Blake2b.Hash(Encoding.UTF8.GetBytes(actualDump), 16));
                var expectedHash = expectedSteps[stepIndex];

                if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
                {
                    Assert.Fail(
                        $"Caso '{caseName}', paso {stepIndex} ({op.GetRawText()}): " +
                        $"huella esperada '{expectedHash}', obtenida '{actualHash}'.\n" +
                        $"Volcado obtenido: {actualDump}");
                }
            }

            var finalDump = StateDump.Dump(agent);
            if (!string.Equals(finalDump, expectedFinal, StringComparison.Ordinal))
            {
                Assert.Fail(
                    $"Caso '{caseName}' final distinto:\n" +
                    $"Esperado: {expectedFinal}\n" +
                    $"Obtenido: {finalDump}");
            }

            var actualEvents = log.Select(e => e.ToJson()).ToList();
            Assert.Equal(expectedEvents.Count, actualEvents.Count);
            for (var evIdx = 0; evIdx < expectedEvents.Count; evIdx++)
            {
                Assert.Equal(expectedEvents[evIdx], actualEvents[evIdx]);
            }
        }
    }

    private static void ExecuteOp(AgentState agent, ContentPack pack, EventLog log, JsonElement op, string opType)
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
