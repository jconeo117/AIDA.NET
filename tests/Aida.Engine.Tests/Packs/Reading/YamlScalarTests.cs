using Aida.Engine.Packs.Reading;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Tests.Packs.Reading;

public sealed class YamlScalarTests
{
    private static YamlScalarNode Plain(string text) => new(text) { Style = ScalarStyle.Plain };

    [Fact]
    public void KindOf_reconoce_tipos_en_escalares_planos()
    {
        Assert.Equal(ScalarKind.Bool, YamlScalar.KindOf(Plain("true")));
        Assert.Equal(ScalarKind.Bool, YamlScalar.KindOf(Plain("false")));
        Assert.Equal(ScalarKind.Bool, YamlScalar.KindOf(Plain("yes")));
        Assert.Equal(ScalarKind.Bool, YamlScalar.KindOf(Plain("no")));
        Assert.Equal(ScalarKind.Bool, YamlScalar.KindOf(Plain("on")));
        Assert.Equal(ScalarKind.Bool, YamlScalar.KindOf(Plain("off")));

        Assert.Equal(ScalarKind.Float, YamlScalar.KindOf(Plain("1.5")));
        Assert.Equal(ScalarKind.Float, YamlScalar.KindOf(Plain(".inf")));
        Assert.Equal(ScalarKind.Float, YamlScalar.KindOf(Plain("-.inf")));
        Assert.Equal(ScalarKind.Float, YamlScalar.KindOf(Plain(".nan")));
        Assert.Equal(ScalarKind.Float, YamlScalar.KindOf(Plain("1:30.5")));

        Assert.Equal(ScalarKind.Int, YamlScalar.KindOf(Plain("42")));
        Assert.Equal(ScalarKind.Int, YamlScalar.KindOf(Plain("0b101")));
        Assert.Equal(ScalarKind.Int, YamlScalar.KindOf(Plain("077")));
        Assert.Equal(ScalarKind.Int, YamlScalar.KindOf(Plain("0x1f")));
        Assert.Equal(ScalarKind.Int, YamlScalar.KindOf(Plain("1:30")));

        Assert.Equal(ScalarKind.Null, YamlScalar.KindOf(Plain("~")));
        Assert.Equal(ScalarKind.Null, YamlScalar.KindOf(Plain("null")));
        Assert.Equal(ScalarKind.Null, YamlScalar.KindOf(Plain("Null")));
        Assert.Equal(ScalarKind.Null, YamlScalar.KindOf(Plain("")));

        Assert.Equal(ScalarKind.Timestamp, YamlScalar.KindOf(Plain("2026-01-01")));

        Assert.Equal(ScalarKind.Text, YamlScalar.KindOf(Plain("hola")));
        Assert.Equal(ScalarKind.Text, YamlScalar.KindOf(Plain("1e5")));

        var quoted = new YamlScalarNode("true") { Style = ScalarStyle.SingleQuoted };
        Assert.Equal(ScalarKind.Text, YamlScalar.KindOf(quoted));
    }

    [Fact]
    public void BoolOf_interpreta_valores_verdaderos_y_falsos()
    {
        Assert.True(YamlScalar.BoolOf("yes"));
        Assert.True(YamlScalar.BoolOf("Yes"));
        Assert.True(YamlScalar.BoolOf("YES"));
        Assert.True(YamlScalar.BoolOf("true"));
        Assert.True(YamlScalar.BoolOf("True"));
        Assert.True(YamlScalar.BoolOf("TRUE"));
        Assert.True(YamlScalar.BoolOf("on"));
        Assert.True(YamlScalar.BoolOf("On"));
        Assert.True(YamlScalar.BoolOf("ON"));

        Assert.False(YamlScalar.BoolOf("no"));
        Assert.False(YamlScalar.BoolOf("false"));
        Assert.False(YamlScalar.BoolOf("off"));
    }

    [Fact]
    public void TryIntOf_convierte_bases_y_rechaza_desbordamientos()
    {
        Assert.True(YamlScalar.TryIntOf("0", out var zero));
        Assert.Equal(0, zero);

        Assert.True(YamlScalar.TryIntOf("+42", out var pos));
        Assert.Equal(42, pos);

        Assert.True(YamlScalar.TryIntOf("-42", out var neg));
        Assert.Equal(-42, neg);

        Assert.True(YamlScalar.TryIntOf("0b101", out var bin));
        Assert.Equal(5, bin);

        Assert.True(YamlScalar.TryIntOf("077", out var oct));
        Assert.Equal(63, oct);

        Assert.True(YamlScalar.TryIntOf("0x1f", out var hex));
        Assert.Equal(31, hex);

        Assert.True(YamlScalar.TryIntOf("0x2a", out var hex2));
        Assert.Equal(42, hex2);

        Assert.True(YamlScalar.TryIntOf("1:02:03", out var sexa));
        Assert.Equal(1 * 3600 + 2 * 60 + 3, sexa);

        Assert.False(YamlScalar.TryIntOf("9999999999999999999999999999999999999999", out _));
    }

    [Fact]
    public void FloatOf_convierte_decimales_infinitos_y_sexagesimales()
    {
        Assert.Equal(12.5, YamlScalar.FloatOf("12.5"));
        Assert.Equal(-0.5, YamlScalar.FloatOf("-0.5"));
        Assert.True(double.IsPositiveInfinity(YamlScalar.FloatOf(".inf")));
        Assert.True(double.IsNegativeInfinity(YamlScalar.FloatOf("-.inf")));
        Assert.True(double.IsNaN(YamlScalar.FloatOf(".nan")));
        Assert.Equal(90.5, YamlScalar.FloatOf("1:30.5"));
    }

    [Fact]
    public void TextOf_devuelve_cadena_vacia_si_nodo_no_tiene_valor()
    {
        var node = new YamlScalarNode();
        Assert.Equal(string.Empty, YamlScalar.TextOf(node));
    }
}
