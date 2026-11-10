using System.Text;
using Aida.Engine.Serialization;

namespace Aida.Engine.Tests.Serialization;

public sealed class CanonicalJsonTests
{
    [Theory]
    [InlineData("simple", "\"simple\"")]
    [InlineData("con \"comillas\" y \\barra\\", "\"con \\\"comillas\\\" y \\\\barra\\\\\"")]
    [InlineData("linea1\nlinea2\r\t\b\f", "\"linea1\\nlinea2\\r\\t\\b\\f\"")]
    [InlineData("parrafo\u2028y\u2029separador", "\"parrafo\\u2028y\\u2029separador\"")]
    [InlineData("\u0001control\u001f", "\"\\u0001control\\u001f\"")]
    public void WriteString_escapa_como_Python(string input, string expected)
    {
        var sb = new StringBuilder();
        CanonicalJson.WriteString(sb, input);
        Assert.Equal(expected, sb.ToString());
    }

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(42L, "42")]
    [InlineData(-100L, "-100")]
    [InlineData(long.MaxValue, "9223372036854775807")]
    [InlineData(long.MinValue, "-9223372036854775808")]
    public void FormatInteger_formatea_como_Python(long value, string expected)
    {
        Assert.Equal(expected, CanonicalJson.FormatInteger(value));
    }

    [Fact]
    public void FormatDouble_maneja_especiales_y_cero()
    {
        Assert.Equal("NaN", CanonicalJson.FormatDouble(double.NaN));
        Assert.Equal("Infinity", CanonicalJson.FormatDouble(double.PositiveInfinity));
        Assert.Equal("-Infinity", CanonicalJson.FormatDouble(double.NegativeInfinity));
        Assert.Equal("0.0", CanonicalJson.FormatDouble(0.0));
        Assert.Equal("-0.0", CanonicalJson.FormatDouble(-0.0));
    }
}
