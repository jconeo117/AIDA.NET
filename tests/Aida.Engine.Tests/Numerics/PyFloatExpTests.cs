using Aida.Engine.Numerics;

namespace Aida.Engine.Tests.Numerics;

public sealed class PyFloatExpTests
{
    [Fact]
    public void Exp_desborda_como_math_exp()
    {
        var ex = Assert.Throws<OverflowException>(() => PyFloat.Exp(710.0));
        Assert.Equal("math range error", ex.Message);
    }

    [Fact]
    public void Exp_de_infinito_no_lanza()
    {
        var result = PyFloat.Exp(double.PositiveInfinity);
        Assert.Equal(double.PositiveInfinity, result);
    }

    [Fact]
    public void Exp_de_menos_infinito_es_cero()
    {
        var result = PyFloat.Exp(double.NegativeInfinity);
        Assert.Equal(0.0, result);
        Assert.Equal(0UL, BitConverter.DoubleToUInt64Bits(result));
    }

    [Fact]
    public void Exp_de_nan_es_nan()
    {
        var result = PyFloat.Exp(double.NaN);
        Assert.True(double.IsNaN(result));
    }
}
