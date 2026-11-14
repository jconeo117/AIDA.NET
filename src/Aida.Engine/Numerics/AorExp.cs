// Portado de ARM optimized-routines, commit 20859d3fd9e797dde592c489c8ef7d383f778620, math/exp.c.
// Copyright (c) 2018-2026, Arm Limited.
// SPDX-License-Identifier: MIT OR Apache-2.0 WITH LLVM-exception (se toma bajo MIT; ver THIRD-PARTY-NOTICES.md).

namespace Aida.Engine.Numerics;

/// <summary>La <c>exp</c> de ARM optimized-routines, la misma que usa glibc desde la 2.28 y, con ella, la 0.1.</summary>
/// <remarks>
/// <para>Las <see cref="Math.FusedMultiplyAdd"/> están donde las pone gcc al compilar glibc con FMA: en todo producto
/// cuyos usos son sumas en su mismo bloque básico. En la rama de subdesbordamiento de <see cref="Special"/>, el
/// producto <c>scale * tmp</c> se usa dos veces y no se fusiona. Medido contra la 0.1: 3 810 263 argumentos iguales;
/// sin esas FMA, 1 752 distintos.</para>
/// <para>Devuelve infinito al desbordar. Convertirlo en error es cosa de <see cref="PyFloat.Exp"/>.</para>
/// </remarks>
internal static class AorExp
{
    /// <summary><c>exp(x)</c> para cualquier <c>double</c>.</summary>
    /// <param name="x">El exponente.</param>
    /// <returns>e elevado a <paramref name="x"/>; infinito si desborda, 0 si subdesborda del todo.</returns>
    internal static double Core(double x)
    {
        unchecked
        {
            uint abstop = (uint)(Bits(x) >> 52) & 0x7ffu;
            if (abstop - 0x3c9u >= 0x408u - 0x3c9u)
            {
                if (abstop - 0x3c9u >= 0x80000000u)
                {
                    return 1.0 + x; // |x| < 2^-54, también el cero
                }

                if (abstop >= 0x409u)
                {
                    if (Bits(x) == Bits(double.NegativeInfinity))
                    {
                        return 0.0;
                    }

                    if (abstop >= 0x7ffu)
                    {
                        return 1.0 + x; // NaN o +inf
                    }

                    return (Bits(x) >> 63) != 0 ? 0.0 : double.PositiveInfinity;
                }

                abstop = 0; // |x| grande pero finito: lo resuelve Special
            }

            double kd = Fma(AorExpTable.InvLn2N, x, AorExpTable.Shift);
            ulong ki = Bits(kd);
            kd -= AorExpTable.Shift;
            double r = Fma(kd, AorExpTable.NegLn2LoN, Fma(kd, AorExpTable.NegLn2HiN, x));
            ulong idx = 2 * (ki % 128);
            ulong top = ki << 45;
            double tail = Dbl(AorExpTable.Tab[(int)idx]);
            ulong sbits = AorExpTable.Tab[(int)idx + 1] + top;
            double r2 = r * r;
            var c = AorExpTable.Poly;
            double tmp = Fma(r2 * r2, Fma(r, c[3], c[2]), Fma(r2, Fma(r, c[1], c[0]), tail + r));
            if (abstop == 0)
            {
                return Special(tmp, sbits, ki);
            }

            double scale = Dbl(sbits);
            return Fma(scale, tmp, scale);
        }
    }

    private static double Special(double tmp, ulong sbits, ulong ki)
    {
        unchecked
        {
            if ((ki & 0x80000000UL) == 0)
            {
                sbits -= 1009UL << 52;
                double scale = Dbl(sbits);
                return Dbl(0x7F00000000000000UL) * Fma(scale, tmp, scale); // 0x1p1009
            }

            sbits += 1022UL << 52;
            double sc = Dbl(sbits);
            double st = sc * tmp; // se usa dos veces: gcc no lo fusiona
            double y = sc + st;
            if (y < 1.0)
            {
                double lo = sc - y + st;
                double hi = 1.0 + y;
                lo = 1.0 - hi + y + lo;
                y = (hi + lo) - 1.0;
                if (y == 0.0)
                {
                    y = 0.0; // nunca -0.0
                }
            }

            return Dbl(0x0010000000000000UL) * y; // 0x1p-1022
        }
    }

    private static double Fma(double a, double b, double c) => Math.FusedMultiplyAdd(a, b, c);

    private static ulong Bits(double value) => BitConverter.DoubleToUInt64Bits(value);

    private static double Dbl(ulong bits) => BitConverter.UInt64BitsToDouble(bits);
}
