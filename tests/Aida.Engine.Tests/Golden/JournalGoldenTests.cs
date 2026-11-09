using System.Text;
using System.Text.Json;
using Aida.Engine.Events;
using Aida.Engine.Ontology;

namespace Aida.Engine.Tests.Golden;

public sealed class JournalGoldenTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void El_log_de_la_plantilla_se_reescribe_igual()
    {
        var lines = GoldenFiles.ReadLines("journals/plantilla-1312-300.jsonl");
        var log = new EventLog();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            var tick = root.GetProperty("tick").GetInt64();
            var kind = GoldenFiles.ParseWireKind(root.GetProperty("kind").GetString()!);
            var owner = new AgentId(root.GetProperty("owner").GetString()!);
            var action = root.TryGetProperty("action", out var actionElem) ? new ActionId(actionElem.GetString()!) : (ActionId?)null;
            var content = root.TryGetProperty("content", out var contentElem) ? contentElem.GetString() : null;

            Dictionary<string, EventValue>? data = null;
            if (root.TryGetProperty("data", out var dataElem))
            {
                data = new Dictionary<string, EventValue>();
                foreach (var prop in dataElem.EnumerateObject())
                {
                    EventValue val = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => EventValue.Of(prop.Value.GetString()!),
                        JsonValueKind.True => EventValue.Of(true),
                        JsonValueKind.False => EventValue.Of(false),
                        JsonValueKind.Number => ParseNumber(prop.Value),
                        _ => throw new InvalidOperationException($"Valor no soportado en data: {prop.Value.ValueKind}"),
                    };
                    data[prop.Name] = val;
                }
            }

            log.Emit(tick, kind, owner, action, data, content);
        }

        var sb = new StringBuilder();
        foreach (var ev in log)
        {
            sb.Append(ev.ToJson()).Append('\n');
        }

        var actualBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var expectedBytes = GoldenFiles.ReadBytes("journals/plantilla-1312-300.jsonl");

        if (!expectedBytes.AsSpan().SequenceEqual(actualBytes))
        {
            var diffs = new List<(int Line, string Expected, string Actual)>();
            var actualLines = sb.ToString().Split('\n');
            var maxLines = Math.Max(lines.Length, actualLines.Length);
            for (var l = 0; l < maxLines; l++)
            {
                var exp = l < lines.Length ? lines[l] : "<fin>";
                var act = l < actualLines.Length ? actualLines[l] : "<fin>";
                if (!string.Equals(exp, act, StringComparison.Ordinal))
                {
                    diffs.Add((l + 1, exp, act));
                }
            }

            Assert.Fail(GoldenFiles.FormatDifferences(diffs));
        }

        Assert.Equal(expectedBytes, actualBytes);
    }

    private static EventValue ParseNumber(JsonElement element)
    {
        var raw = element.GetRawText();
        if (!raw.Contains('.') && !raw.Contains('e') && !raw.Contains('E'))
        {
            return EventValue.Of(element.GetInt64());
        }

        return EventValue.Of(element.GetDouble());
    }
}
