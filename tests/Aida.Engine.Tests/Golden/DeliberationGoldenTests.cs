using System.Globalization;
using System.Text.Json;
using Aida.Engine.Deliberation;
using Aida.Engine.Events;
using Aida.Engine.Ontology;
using Aida.Engine.Randomness;
using Aida.Engine.State;

namespace Aida.Engine.Tests.Golden;

public sealed class DeliberationGoldenTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void La_deliberacion_coincide_con_la_01()
    {
        var stateCases = GoldenFiles.ReadLines("state/cases.jsonl")
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonDocument.Parse(l))
            .ToDictionary(
                d => d.RootElement.GetProperty("name").GetString()!,
                d => d.RootElement.GetProperty("ops").EnumerateArray().ToList());

        var lines = GoldenFiles.ReadLines("deliberation/cases.jsonl");

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
            var seed = root.GetProperty("seed").GetInt64();
            var steps = root.GetProperty("steps").EnumerateArray().ToList();
            var ops = stateCases[caseName];

            var pack = StateReplay.GetPack(packName);
            var agentSpec = pack.Agents.First(a => a.Id.Value == agentId);
            var agent = AgentState.FromSpec(pack, agentSpec);
            var log = new EventLog();
            var rngSource = new RngSource(seed);

            for (var stepIndex = 0; stepIndex < ops.Count; stepIndex++)
            {
                var op = ops[stepIndex];
                var opType = op.GetProperty("op").GetString()!;
                StateReplay.ExecuteOp(agent, pack, log, op, opType);

                var stepElem = steps[stepIndex];
                var step = stepElem.GetProperty("step").GetInt32();
                var tick = stepElem.GetProperty("tick").GetInt64();
                var execution = new ExecutionState(agent, tick, rngSource);

                // 1. Critical needs
                var expectedCriticalNeeds = stepElem.GetProperty("critical_needs")
                    .EnumerateArray()
                    .Select(e => e.GetString()!)
                    .ToList();
                var actualCriticalNeeds = CriticalNeeds.All(execution, pack)
                    .Select(n => n.Value)
                    .ToList();
                Assert.True(
                    expectedCriticalNeeds.SequenceEqual(actualCriticalNeeds),
                    $"Caso {caseName}, paso {step}: critical_needs distinto. Esperado [{string.Join(',', expectedCriticalNeeds)}], obtenido [{string.Join(',', actualCriticalNeeds)}]");

                // 2. Worst critical need
                var expectedCriticalNeed = stepElem.GetProperty("critical_need").GetString();
                var actualCriticalNeed = CriticalNeeds.Worst(execution, pack)?.Value;
                Assert.True(
                    expectedCriticalNeed == actualCriticalNeed,
                    $"Caso {caseName}, paso {step}: critical_need distinto: esperado '{expectedCriticalNeed}', obtenido '{actualCriticalNeed}'");

                // 3. Queries
                var queries = stepElem.GetProperty("queries").EnumerateArray().ToList();
                for (var q = 0; q < queries.Count; q++)
                {
                    var qElem = queries[q];
                    var mustStr = qElem.GetProperty("must").GetString();
                    var must = mustStr is not null ? new NeedId(mustStr) : (NeedId?)null;
                    var forbidden = qElem.GetProperty("forbidden")
                        .EnumerateArray()
                        .Select(e => new ActionId(e.GetString()!))
                        .ToHashSet();

                    // Compara descartadas
                    var expectedDiscarded = qElem.GetProperty("discarded")
                        .EnumerateObject()
                        .ToDictionary(p => p.Name, p => p.Value.GetString()!);
                    var (_, actualDiscarded) = Deliberator.Deliberate(execution, pack, must, forbidden);

                    Assert.True(
                        expectedDiscarded.Count == actualDiscarded.Count,
                        $"Caso {caseName}, paso {step}, consulta {q}, campo discarded.Count: esperado {expectedDiscarded.Count}, obtenido {actualDiscarded.Count}");

                    foreach (var (act, door) in expectedDiscarded)
                    {
                        var actId = new ActionId(act);
                        Assert.True(
                            actualDiscarded.TryGetValue(actId, out var actualDoor) && actualDoor == door,
                            $"Caso {caseName}, paso {step}, consulta {q}, campo discarded[{act}]: esperado '{door}', obtenido '{actualDoor}'");
                    }

                    // Compara decisión
                    var expectedDecision = qElem.GetProperty("decision");
                    var actualDecision = Deliberator.Decide(execution, pack, must, forbidden);

                    if (expectedDecision.ValueKind == JsonValueKind.Null)
                    {
                        Assert.True(
                            actualDecision is null,
                            $"Caso {caseName}, paso {step}, consulta {q}: decisión esperada null, obtenida '{actualDecision?.Action.Value}'");
                    }
                    else
                    {
                        Assert.True(
                            actualDecision is not null,
                            $"Caso {caseName}, paso {step}, consulta {q}: decisión esperada no-null, obtenida null");

                        var expectedAction = expectedDecision.GetProperty("action").GetString()!;
                        var expectedUtility = ulong.Parse(expectedDecision.GetProperty("utility").GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        var expectedBenefit = ulong.Parse(expectedDecision.GetProperty("benefit").GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        var expectedCost = ulong.Parse(expectedDecision.GetProperty("cost").GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        var expectedNovelty = ulong.Parse(expectedDecision.GetProperty("novelty").GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                        Assert.True(
                            actualDecision.Action.Value == expectedAction,
                            $"Caso {caseName}, paso {step}, consulta {q}, campo action: esperado '{expectedAction}', obtenido '{actualDecision.Action.Value}'");
                        Assert.True(
                            BitConverter.DoubleToUInt64Bits(actualDecision.Utility) == expectedUtility,
                            $"Caso {caseName}, paso {step}, consulta {q}, campo utility: esperado 0x{expectedUtility:X16}, obtenido 0x{BitConverter.DoubleToUInt64Bits(actualDecision.Utility):X16}");
                        Assert.True(
                            BitConverter.DoubleToUInt64Bits(actualDecision.Benefit) == expectedBenefit,
                            $"Caso {caseName}, paso {step}, consulta {q}, campo benefit: esperado 0x{expectedBenefit:X16}, obtenido 0x{BitConverter.DoubleToUInt64Bits(actualDecision.Benefit):X16}");
                        Assert.True(
                            BitConverter.DoubleToUInt64Bits(actualDecision.Cost) == expectedCost,
                            $"Caso {caseName}, paso {step}, consulta {q}, campo cost: esperado 0x{expectedCost:X16}, obtenido 0x{BitConverter.DoubleToUInt64Bits(actualDecision.Cost):X16}");
                        Assert.True(
                            BitConverter.DoubleToUInt64Bits(actualDecision.Novelty) == expectedNovelty,
                            $"Caso {caseName}, paso {step}, consulta {q}, campo novelty: esperado 0x{expectedNovelty:X16}, obtenido 0x{BitConverter.DoubleToUInt64Bits(actualDecision.Novelty):X16}");
                    }
                }
            }
        }
    }
}
