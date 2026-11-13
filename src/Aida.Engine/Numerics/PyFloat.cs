namespace Aida.Engine.Numerics;

/// <summary>Las operaciones de <c>float</c> de Python que el motor necesita, con su semántica exacta.</summary>
/// <remarks>
/// <para><see cref="Pow"/> es la única puerta de potencias del motor (ADR 0026): reproduce los casos especiales de
/// <c>float_pow</c> de CPython 3.12 (<c>Objects/floatobject.c:745-862</c>) y delega el resto en
/// <see cref="AorPow.Core"/>, el mismo <c>pow</c> que usa la 0.1.</para>
/// <para><see cref="Min"/> y <see cref="Max"/> son <c>min(a, b)</c> y <c>max(a, b)</c> de Python, que no son
/// <see cref="Math.Min(double, double)"/> ni <see cref="Math.Max(double, double)"/>: Python devuelve el primero
/// salvo que el segundo sea estrictamente menor (o mayor), así que <c>max(0.0, -0.0)</c> es <c>0.0</c> y
/// <c>min(-0.0, 0.0)</c> es <c>-0.0</c>, y con NaN gana el orden de los argumentos.</para>
/// </remarks>
internal static class PyFloat
{
    /// <summary><c>x ** y</c> de Python para dos <c>float</c>.</summary>
    /// <param name="x">La base.</param>
    /// <param name="y">El exponente.</param>
    /// <returns>La potencia, bit a bit igual que en la 0.1.</returns>
    /// <exception cref="DivideByZeroException">Si <paramref name="x"/> es cero y <paramref name="y"/> negativo
    /// (<c>ZeroDivisionError</c> en Python).</exception>
    /// <exception cref="OverflowException">Si el resultado desborda (<c>OverflowError</c> en Python).</exception>
    /// <exception cref="ArithmeticException">Si <paramref name="x"/> es negativo y <paramref name="y"/> no es
    /// entero: Python devolvería un <c>complex</c>, que el motor no tiene.</exception>
    internal static double Pow(double x, double y)
    {
        if (y == 0.0)
        {
            return 1.0;
        }

        if (double.IsNaN(x))
        {
            return x;
        }

        if (double.IsNaN(y))
        {
            return x == 1.0 ? 1.0 : y;
        }

        if (double.IsInfinity(y))
        {
            var ax = Math.Abs(x);
            if (ax == 1.0)
            {
                return 1.0;
            }

            return (y > 0.0) == (ax > 1.0) ? Math.Abs(y) : 0.0;
        }

        if (double.IsInfinity(x))
        {
            var odd = IsOddInteger(y);
            if (y > 0.0)
            {
                return odd ? x : Math.Abs(x);
            }

            return odd ? Math.CopySign(0.0, x) : 0.0;
        }

        if (x == 0.0)
        {
            if (y < 0.0)
            {
                throw new DivideByZeroException("0.0 cannot be raised to a negative power");
            }

            return IsOddInteger(y) ? x : 0.0;
        }

        var negate = false;
        if (x < 0.0)
        {
            if (y != Math.Floor(y))
            {
                throw new ArithmeticException("la 0.1 devolvería un complex: base negativa con exponente fraccionario");
            }

            x = -x;
            negate = IsOddInteger(y);
        }

        if (x == 1.0)
        {
            return negate ? -1.0 : 1.0;
        }

        var result = AorPow.Core(x, y);
        if (double.IsInfinity(result))
        {
            throw new OverflowException("(34, 'Numerical result out of range')");
        }

        return negate ? -result : result;
    }

    /// <summary><c>min(a, b)</c> de Python.</summary>
    /// <param name="a">El primero.</param>
    /// <param name="b">El segundo.</param>
    /// <returns><paramref name="b"/> si es estrictamente menor que <paramref name="a"/>; si no, <paramref name="a"/>.</returns>
    internal static double Min(double a, double b) => b < a ? b : a;

    /// <summary><c>max(a, b)</c> de Python.</summary>
    /// <param name="a">El primero.</param>
    /// <param name="b">El segundo.</param>
    /// <returns><paramref name="b"/> si es estrictamente mayor que <paramref name="a"/>; si no, <paramref name="a"/>.</returns>
    internal static double Max(double a, double b) => b > a ? b : a;

    // DOUBLE_IS_ODD_INTEGER de CPython: fmod(fabs(x), 2.0) == 1.0. El % de double en C# es fmod.
    private static bool IsOddInteger(double value) => Math.Abs(value) % 2.0 == 1.0;
}
