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

            var pack = StateReplay.GetPack(packName);
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

            var pack = StateReplay.GetPack(packName);
            var agentSpec = pack.Agents.First(a => a.Id.Value == agentId);
            var agent = AgentState.FromSpec(pack, agentSpec);
            var log = new EventLog();

            for (var stepIndex = 0; stepIndex < ops.Count; stepIndex++)
            {
                var op = ops[stepIndex];
                var opType = op.GetProperty("op").GetString()!;

                StateReplay.ExecuteOp(agent, pack, log, op, opType);

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
}
