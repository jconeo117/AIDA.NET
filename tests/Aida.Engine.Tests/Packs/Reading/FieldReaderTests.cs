using Aida.Engine.Ontology;
using Aida.Engine.Packs.Reading;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Tests.Packs.Reading;

public sealed class FieldReaderTests
{
    private static YamlScalarNode Plain(string text) => new(text) { Style = ScalarStyle.Plain };

    [Fact]
    public void TryFloat_convierte_distintos_tipos_y_textos()
    {
        var problems = new ProblemList();

        Assert.True(FieldReader.TryFloat(Plain("1.5"), "f", problems, out var v1));
        Assert.Equal(1.5, v1);

        Assert.True(FieldReader.TryFloat(Plain("42"), "f", problems, out var v2));
        Assert.Equal(42.0, v2);

        Assert.True(FieldReader.TryFloat(Plain("true"), "f", problems, out var v3));
        Assert.Equal(1.0, v3);

        Assert.True(FieldReader.TryFloat(Plain("false"), "f", problems, out var v4));
        Assert.Equal(0.0, v4);

        var quoted = new YamlScalarNode(" 3.14 ") { Style = ScalarStyle.SingleQuoted };
        Assert.True(FieldReader.TryFloat(quoted, "f", problems, out var v5));
        Assert.Equal(3.14, v5);

        var special = new YamlScalarNode("inf") { Style = ScalarStyle.SingleQuoted };
        Assert.True(FieldReader.TryFloat(special, "f", problems, out var v6));
        Assert.True(double.IsPositiveInfinity(v6));

        var nanQuoted = new YamlScalarNode("nan") { Style = ScalarStyle.SingleQuoted };
        Assert.True(FieldReader.TryFloat(nanQuoted, "f", problems, out var v7));
        Assert.True(double.IsNaN(v7));

        var invalid = new YamlScalarNode("abc") { Style = ScalarStyle.SingleQuoted };
        Assert.False(FieldReader.TryFloat(invalid, "f", problems, out _));
        Assert.False(FieldReader.TryFloat(new YamlMappingNode(), "f", problems, out _));
    }

    [Fact]
    public void TryInt_convierte_enteros_decimales_enteros_y_textos()
    {
        var problems = new ProblemList();

        Assert.True(FieldReader.TryInt(Plain("42"), "i", problems, out var v1));
        Assert.Equal(42, v1);

        Assert.True(FieldReader.TryInt(Plain("2.0"), "i", problems, out var v2));
        Assert.Equal(2, v2);

        Assert.False(FieldReader.TryInt(Plain("2.5"), "i", problems, out _));

        Assert.True(FieldReader.TryInt(Plain("true"), "i", problems, out var v3));
        Assert.Equal(1, v3);

        Assert.True(FieldReader.TryInt(Plain("false"), "i", problems, out var v4));
        Assert.Equal(0, v4);

        var quoted = new YamlScalarNode(" 99 ") { Style = ScalarStyle.SingleQuoted };
        Assert.True(FieldReader.TryInt(quoted, "i", problems, out var v5));
        Assert.Equal(99, v5);

        Assert.False(FieldReader.TryInt(new YamlScalarNode("abc") { Style = ScalarStyle.SingleQuoted }, "i", problems, out _));
    }

    [Fact]
    public void TryBool_convierte_booleanos_enteros_y_palabras()
    {
        var problems = new ProblemList();

        Assert.True(FieldReader.TryBool(Plain("true"), "b", problems, out var v1));
        Assert.True(v1);

        Assert.True(FieldReader.TryBool(Plain("1"), "b", problems, out var v2));
        Assert.True(v2);

        Assert.True(FieldReader.TryBool(Plain("0"), "b", problems, out var v3));
        Assert.False(v3);

        Assert.False(FieldReader.TryBool(Plain("2"), "b", problems, out _));

        foreach (var word in new[] { "on", "t", "true", "y", "yes", "1" })
        {
            var node = new YamlScalarNode(word) { Style = ScalarStyle.SingleQuoted };
            Assert.True(FieldReader.TryBool(node, "b", problems, out var val));
            Assert.True(val, $"Falló para {word}");
        }

        foreach (var word in new[] { "off", "f", "false", "n", "no", "0" })
        {
            var node = new YamlScalarNode(word) { Style = ScalarStyle.SingleQuoted };
            Assert.True(FieldReader.TryBool(node, "b", problems, out var val));
            Assert.False(val, $"Falló para {word}");
        }

        var invalid = new YamlScalarNode("quizas") { Style = ScalarStyle.SingleQuoted };
        Assert.False(FieldReader.TryBool(invalid, "b", problems, out _));
    }

    [Fact]
    public void Restricciones_numericas_y_de_coleccion()
    {
        var problems = new ProblemList();

        var inRange = Constraints.FloatIn(0.0, 1.0);
        Assert.True(inRange(Plain("0.0"), "x", problems, out _));
        Assert.True(inRange(Plain("1.0"), "x", problems, out _));
        Assert.False(inRange(Plain("-0.1"), "x", problems, out _));
        Assert.False(inRange(Plain("1.1"), "x", problems, out _));
        Assert.False(inRange(Plain(".nan"), "x", problems, out _));

        var halfOpen = Constraints.FloatInHalfOpen(0.0, 1.0);
        Assert.False(halfOpen(Plain("0.0"), "x", problems, out _));
        Assert.True(halfOpen(Plain("1.0"), "x", problems, out _));
        Assert.True(halfOpen(Plain("0.5"), "x", problems, out _));

        var atLeast = Constraints.FloatAtLeast(10.0);
        Assert.True(atLeast(Plain("10.0"), "x", problems, out _));
        Assert.False(atLeast(Plain("9.9"), "x", problems, out _));

        var above = Constraints.FloatAbove(0.0);
        Assert.False(above(Plain("0.0"), "x", problems, out _));
        Assert.True(above(Plain("0.001"), "x", problems, out _));

        var intAtLeast = Constraints.IntAtLeast(1);
        Assert.True(intAtLeast(Plain("1"), "x", problems, out _));
        Assert.False(intAtLeast(Plain("0"), "x", problems, out _));
    }

    [Fact]
    public void TryMapping_y_TryText_validan_estructura()
    {
        var problems = new ProblemList();

        Assert.True(FieldReader.TryMapping(new YamlMappingNode(), "m", problems, out _));
        Assert.False(FieldReader.TryMapping(Plain("no"), "m", problems, out _));

        var textNode = new YamlScalarNode("cadena") { Style = ScalarStyle.SingleQuoted };
        Assert.True(FieldReader.TryText(textNode, "t", problems, out var txt));
        Assert.Equal("cadena", txt);

        var plainBool = Plain("true");
        Assert.False(FieldReader.TryText(plainBool, "t", problems, out _));
    }
}
