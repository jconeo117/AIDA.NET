using System.Text;
using Aida.Engine.Serialization;

namespace Aida.Engine.Tests.Serialization;

public sealed class PythonTextTests
{
    [Fact]
    public void Repr_como_Python()
    {
        Assert.Equal("'a'", PythonText.Repr("a"));
        Assert.Equal("\"it's\"", PythonText.Repr("it's"));
        Assert.Equal("'a\\'b\"c'", PythonText.Repr("a'b\"c"));
        Assert.Equal("'\\t'", PythonText.Repr("\t"));
        Assert.Equal("'ñ'", PythonText.Repr("ñ"));
        Assert.Equal("'\\x85'", PythonText.Repr("\u0085"));
        Assert.Equal("'\\u200b'", PythonText.Repr("\u200b"));
        Assert.Equal("'\\ue000'", PythonText.Repr("\ue000"));
        Assert.Equal("'😀'", PythonText.Repr("😀"));
    }

    [Fact]
    public void Repr_escapa_caracteres_especiales_y_de_control()
    {
        Assert.Equal("'\\r'", PythonText.Repr("\r"));
        Assert.Equal("'\\n'", PythonText.Repr("\n"));
        Assert.Equal("'\\\\'", PythonText.Repr("\\"));
        Assert.Equal("'\\x00'", PythonText.Repr("\0"));
        Assert.Equal("'\\x1f'", PythonText.Repr("\u001f"));
        Assert.Equal("'\\x7f'", PythonText.Repr("\u007f"));
        Assert.Equal("'\\xa0'", PythonText.Repr("\u00a0")); // non-breaking space (SpaceSeparator) -> not printable
        Assert.Equal("'\\U000e0001'", PythonText.Repr(char.ConvertFromUtf32(0x000e0001)));
        Assert.Equal("'\"'", PythonText.Repr("\"")); // only double quote -> uses single quotes
    }

    [Fact]
    public void Float_formatea_casos_especiales()
    {
        Assert.Equal("nan", PythonText.Float(double.NaN));
        Assert.Equal("inf", PythonText.Float(double.PositiveInfinity));
        Assert.Equal("-inf", PythonText.Float(double.NegativeInfinity));
        Assert.Equal("1.0", PythonText.Float(1.0));
        Assert.Equal("0.0", PythonText.Float(0.0));
        Assert.Equal("-0.0", PythonText.Float(-0.0));
    }

    [Fact]
    public void Round4_como_Python()
    {
        Assert.Equal(0.1235, PythonText.Round4(0.123456));
        Assert.Equal(0.0312, PythonText.Round4(0.03125));
        Assert.Equal(-0.0312, PythonText.Round4(-0.03125));
        Assert.Equal(0.0001, PythonText.Round4(0.00005));
        Assert.Equal(2.5, PythonText.Round4(2.5));
        Assert.Equal(0.0, PythonText.Round4(0.0));
        Assert.True(double.IsNaN(PythonText.Round4(double.NaN)));
        Assert.True(double.IsPositiveInfinity(PythonText.Round4(double.PositiveInfinity)));
        Assert.True(double.IsNegativeInfinity(PythonText.Round4(double.NegativeInfinity)));
    }
}
