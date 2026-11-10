using System.Globalization;
using System.Text.Json;
using Aida.Engine.Events;
using Aida.Engine.Ontology;

namespace Aida.Engine.Tests.Golden;

public sealed class EventsGoldenTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void Los_casos_de_eventos_se_escriben_como_en_la_01()
    {
        var lines = GoldenFiles.ReadLines("events/cases.jsonl");
        var log = new EventLog();
        var diffs = new List<(int Line, string Expected, string Actual)>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var expectedLine = root.GetProperty("line").GetString()!;
            var input = root.GetProperty("input");

            var tick = input.GetProperty("tick").GetInt64();
            var kind = GoldenFiles.ParseWireKind(input.GetProperty("kind").GetString()!);
            var owner = new AgentId(input.GetProperty("owner").GetString()!);
            var action = input.TryGetProperty("action", out var actionElem) ? new ActionId(actionElem.GetString()!) : (ActionId?)null;
            var content = input.TryGetProperty("content", out var contentElem) ? contentElem.GetString() : null;

            Dictionary<string, EventValue>? data = null;
            if (input.TryGetProperty("data", out var dataElem))
            {
                data = new Dictionary<string, EventValue>();
                foreach (var prop in dataElem.EnumerateObject())
                {
                    var typeStr = prop.Value[0].GetString()!;
                    var valueElem = prop.Value[1];
                    EventValue evVal = typeStr switch
                    {
                        "str" => EventValue.Of(valueElem.GetString()!),
                        "int" => EventValue.Of(valueElem.GetInt64()),
                        "bool" => EventValue.Of(valueElem.GetBoolean()),
                        "float" => EventValue.Of(BitConverter.UInt64BitsToDouble(
                            ulong.Parse(valueElem.GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture))),
                        _ => throw new InvalidOperationException($"Tipo de valor desconocido en dorado: {typeStr}"),
                    };
                    data[prop.Name] = evVal;
                }
            }

            var recorded = log.Emit(tick, kind, owner, action, data, content);
            var actualLine = recorded.ToJson();

            if (recorded.Seq != i || !string.Equals(actualLine, expectedLine, StringComparison.Ordinal))
            {
                diffs.Add((i + 1, expectedLine, $"seq={recorded.Seq} json={actualLine}"));
            }
        }

        Assert.True(diffs.Count == 0, GoldenFiles.FormatDifferences(diffs));
    }
}
