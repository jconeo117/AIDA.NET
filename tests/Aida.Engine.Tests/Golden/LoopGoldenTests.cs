using System.Text;
using System.Text.Json;
using Aida.Engine.Loop;
using Aida.Engine.Packs;
using Aida.Engine.Ports;

namespace Aida.Engine.Tests.Golden;

public sealed class LoopGoldenTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void El_bucle_reproduce_los_registros_de_la_01()
    {
        var scriptsPath = Path.Combine(GoldenFiles.GoldenDir, "journals", "scripts.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(scriptsPath));
        var scripts = doc.RootElement.EnumerateArray();
        var failures = new List<string>();
        var total = 0;

        foreach (var sc in scripts)
        {
            total++;
            var name = sc.GetProperty("name").GetString()!;
            var packRel = sc.GetProperty("pack").GetString()!;
            var agentId = sc.GetProperty("agent").GetString()!;
            var seed = sc.GetProperty("seed").GetInt64();
            var ticks = sc.GetProperty("ticks").GetInt32();
            var capacity = sc.GetProperty("capacity").GetInt64();
            var policy = sc.GetProperty("policy").GetString()!;

            var packPath = Path.Combine(GoldenFiles.RepoRoot, packRel);
            var pack = PackLoader.Load(packPath);
            var agentSpec = pack.Agents.First(a => a.Id.Value == agentId);

            IIntentClassifier? classifier = null;
            if (sc.TryGetProperty("classifier", out var clfElem) && clfElem.ValueKind == JsonValueKind.Object)
            {
                var table = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var prop in clfElem.EnumerateObject())
                {
                    table[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.GetString();
                }

                classifier = new TableClassifier(table);
            }

            var loop = AgentLoop.Start(pack, agentSpec, seed, capacity, classifier);

            if (sc.TryGetProperty("inbox", out var inboxElem))
            {
                foreach (var item in inboxElem.EnumerateArray())
                {
                    var t = item[0].GetInt64();
                    var content = item[1].GetString()!;
                    loop.Inbox.Push(t, agentSpec.Id, content);
                }
            }

            for (var tick = 0; tick < ticks; tick++)
            {
                loop.Step();
                if (policy == "mod3")
                {
                    foreach (var actionId in loop.Authority.Pending.Keys.ToList())
                    {
                        var m = loop.Execution.Tick % 3;
                        if (m == 0)
                        {
                            loop.Deny(actionId);
                        }
                        else if (m == 1)
                        {
                            loop.Approve(actionId);
                        }
                    }
                }
            }

            var expectedBytes = GoldenFiles.ReadBytes($"journals/{name}.jsonl");
            var sb = new StringBuilder();
            foreach (var ev in loop.Log)
            {
                sb.Append(ev.ToJson()).Append('\n');
            }

            var actualBytes = Encoding.UTF8.GetBytes(sb.ToString());

            if (!expectedBytes.AsSpan().SequenceEqual(actualBytes))
            {
                var expectedText = Encoding.UTF8.GetString(expectedBytes);
                var actualText = sb.ToString();
                var expLines = expectedText.Split('\n');
                var actLines = actualText.Split('\n');
                var max = Math.Max(expLines.Length, actLines.Length);
                var diffLine = -1;
                var diffExp = string.Empty;
                var diffAct = string.Empty;

                for (var l = 0; l < max; l++)
                {
                    var exp = l < expLines.Length ? expLines[l] : "<fin>";
                    var act = l < actLines.Length ? actLines[l] : "<fin>";
                    if (!string.Equals(exp, act, StringComparison.Ordinal))
                    {
                        diffLine = l + 1;
                        diffExp = exp;
                        diffAct = act;
                        break;
                    }
                }

                failures.Add($"[{name}] Primera diferencia en línea {diffLine}:\n  Esperado: {diffExp}\n  Obtenido: {diffAct}");
            }
        }

        if (failures.Count > 0)
        {
            Assert.Fail($"Fallaron {failures.Count} de {total} registros dorados:\n\n" + string.Join("\n\n", failures));
        }
    }

    private sealed class TableClassifier(IReadOnlyDictionary<string, string?> table) : IIntentClassifier
    {
        public string? Classify(string utterance) =>
            table.TryGetValue(utterance, out var intent) ? intent : null;
    }
}
