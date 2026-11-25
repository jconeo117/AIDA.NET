using System.Globalization;
using System.Text.Json;
using Aida.Engine.Text;
using Xunit;

namespace Aida.Engine.Tests.Golden;

public sealed class TextGoldenTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public void TextTokens_y_WordBag_coinciden_con_la_01()
    {
        var lines = GoldenFiles.ReadLines("intent/cases.jsonl");

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var kind = root.GetProperty("kind").GetString();

            if (kind == "textmatch_text")
            {
                var text = root.GetProperty("text").GetString()!;
                var expectedTokens = root.GetProperty("tokens").EnumerateArray()
                    .Select(t => t.GetString()!).ToList();
                var expectedSpans = root.GetProperty("spans").EnumerateArray()
                    .Select(s => new TokenSpan(
                        s.GetProperty("token").GetString()!,
                        s.GetProperty("start").GetInt32(),
                        s.GetProperty("end").GetInt32()))
                    .ToList();

                var actualTokens = TextTokens.Tokenize(text);
                var actualSpans = TextTokens.Spans(text);

                Assert.Equal(expectedTokens, actualTokens);
                Assert.Equal(expectedSpans, actualSpans);

                var expectedRaw = root.GetProperty("vector_raw").EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.Ordinal);
                var actualRaw = WordBag.Vectorize(text);
                Assert.Equal(expectedRaw, actualRaw);

                var stopSet = root.GetProperty("stopwords").EnumerateArray()
                    .Select(s => s.GetString()!)
                    .ToHashSet(StringComparer.Ordinal);
                var expectedFiltered = root.GetProperty("vector_stopwords").EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.Ordinal);
                var actualFiltered = WordBag.Vectorize(text, stopSet);
                Assert.Equal(expectedFiltered, actualFiltered);
            }
            else if (kind == "textmatch_cosine")
            {
                var vecA = root.GetProperty("vector_a").EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.Ordinal);
                var vecB = root.GetProperty("vector_b").EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.Ordinal);

                var expectedHex = root.GetProperty("cosine_hex").GetString()!;
                var expectedCosine = root.GetProperty("cosine").GetDouble();

                var actualCosine = WordBag.Cosine(vecA, vecB);
                var actualBits = BitConverter.DoubleToUInt64Bits(actualCosine);
                var actualHex = $"{actualBits:X16}";

                Assert.Equal(expectedHex, actualHex);
                Assert.Equal(expectedCosine, actualCosine);
            }
        }
    }
}
