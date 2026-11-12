using Aida.Engine.Numerics;

namespace Aida.Engine.Tests.Numerics;

public sealed class PyFloatTests
{
    [Fact]
    public void Min_y_Max_siguen_la_semantica_de_Python_con_ceros_con_signo_y_nan()
    {
        // En Python, min(a, b) devuelve b solo si b < a estrictamente.
        // max(a, b) devuelve b solo si b > a estrictamente.
        // Como 0.0 == -0.0, nunca es estrictamente menor ni mayor: gana el primer argumento.
        Assert.Equal(0UL, BitConverter.DoubleToUInt64Bits(PyFloat.Min(0.0, -0.0)));
        Assert.Equal(0x8000000000000000UL, BitConverter.DoubleToUInt64Bits(PyFloat.Min(-0.0, 0.0)));

        Assert.Equal(0UL, BitConverter.DoubleToUInt64Bits(PyFloat.Max(0.0, -0.0)));
        Assert.Equal(0x8000000000000000UL, BitConverter.DoubleToUInt64Bits(PyFloat.Max(-0.0, 0.0)));

        // Con NaN: si a es NaN, b < a y b > a son falsos, por lo que gana a.
        // Si b es NaN, b < a y b > a son falsos, por lo que gana a.
        Assert.True(double.IsNaN(PyFloat.Min(double.NaN, 1.0)));
        Assert.Equal(1.0, PyFloat.Min(1.0, double.NaN));

        Assert.True(double.IsNaN(PyFloat.Max(double.NaN, 1.0)));
        Assert.Equal(1.0, PyFloat.Max(1.0, double.NaN));
    }

    [Fact]
    public void Pow_reproduce_los_casos_especiales_medidos_de_la_01()
    {
        // 0.0 ** -1.0 -> ZeroDivisionError: 0.0 cannot be raised to a negative power
        var divEx = Assert.Throws<DivideByZeroException>(() => PyFloat.Pow(0.0, -1.0));
        Assert.Equal("0.0 cannot be raised to a negative power", divEx.Message);

        // 10.0 ** 308.5 -> OverflowError: (34, 'Numerical result out of range')
        var overEx = Assert.Throws<OverflowException>(() => PyFloat.Pow(10.0, 308.5));
        Assert.Equal("(34, 'Numerical result out of range')", overEx.Message);

        // (-8.0) ** (1/3) -> complex
        var arithEx = Assert.Throws<ArithmeticException>(() => PyFloat.Pow(-8.0, 1.0 / 3.0));
        Assert.Contains("complex", arithEx.Message, StringComparison.Ordinal);

        // 1.5 ** -1800.0 -> subnormal y sin error (0x0000000000218862)
        var subnormal = PyFloat.Pow(1.5, -1800.0);
        Assert.True(double.IsSubnormal(subnormal));
        Assert.Equal(0x0000000000218862UL, BitConverter.DoubleToUInt64Bits(subnormal));

        // (-0.0) ** 3.0 -> -0.0
        var negZeroCube = PyFloat.Pow(-0.0, 3.0);
        Assert.Equal(0x8000000000000000UL, BitConverter.DoubleToUInt64Bits(negZeroCube));

        // (-0.0) ** 2.0 -> 0.0
        var negZeroSquare = PyFloat.Pow(-0.0, 2.0);
        Assert.Equal(0UL, BitConverter.DoubleToUInt64Bits(negZeroSquare));

        // nan ** 0.0 -> 1.0
        Assert.Equal(1.0, PyFloat.Pow(double.NaN, 0.0));

        // 1.0 ** nan -> 1.0
        Assert.Equal(1.0, PyFloat.Pow(1.0, double.NaN));
    }

    [Fact]
    public void Pow_de_dos_elevado_a_medio_coincide_con_raiz_cuadrada()
    {
        var powSqrt = PyFloat.Pow(2.0, 0.5);
        var mathSqrt = Math.Sqrt(2.0);
        Assert.Equal(BitConverter.DoubleToUInt64Bits(mathSqrt), BitConverter.DoubleToUInt64Bits(powSqrt));
    }
}
