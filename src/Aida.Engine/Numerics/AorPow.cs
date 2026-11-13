// Portado de ARM optimized-routines, commit 20859d3fd9e797dde592c489c8ef7d383f778620, math/pow.c.
// Copyright (c) 2018-2026, Arm Limited.
// SPDX-License-Identifier: MIT OR Apache-2.0 WITH LLVM-exception (se toma bajo MIT; ver THIRD-PARTY-NOTICES.md).

namespace Aida.Engine.Numerics;

/// <summary>El <c>pow</c> de ARM optimized-routines, el mismo que usa glibc desde la 2.28 y, con él, la 0.1.</summary>
/// <remarks>
/// <para>Solo el camino que llama <c>float_pow</c> de CPython: <c>x</c> finito, positivo y distinto de 1, e
/// <c>y</c> finito y distinto de cero. Los casos especiales los resuelve <see cref="PyFloat.Pow"/> antes (ADR 0026).</para>
/// <para>Las <see cref="Math.FusedMultiplyAdd"/> están exactamente donde las pone gcc al compilar glibc con FMA: en todo
/// producto cuyos usos son sumas en su mismo bloque básico. En la rama de subdesbordamiento de
/// <see cref="Special"/>, el producto <c>scale * tmp</c> se usa en dos bloques y no se fusiona. Medido contra la 0.1:
/// 3 959 357 pares iguales; sin esas FMA, 629 distintos en 1 059 357.</para>
/// </remarks>
internal static class AorPow
{
    private const ulong Off = 0x3fe6955500000000UL;

    /// <summary><c>pow(x, y)</c> para <c>x</c> finito, positivo y distinto de 1, e <c>y</c> finito y no nulo.</summary>
    /// <param name="x">La base.</param>
    /// <param name="y">El exponente.</param>
    /// <returns>La potencia, o infinito positivo si desborda (quien llama lo convierte en error).</returns>
    internal static double Core(double x, double y)
    {
        ulong ix = Bits(x), iy = Bits(y);
        uint topx = (uint)(ix >> 52), topy = (uint)(iy >> 52);
        unchecked
        {
            if (topx - 0x001u >= 0x7ffu - 0x001u || (topy & 0x7ffu) - 0x3beu >= 0x43eu - 0x3beu)
            {
                if ((topy & 0x7ffu) - 0x3beu >= 0x43eu - 0x3beu)
                {
                    if ((topy & 0x7ffu) < 0x3beu)
                    {
                        return ix > Bits(1.0) ? 1.0 + y : 1.0 - y;
                    }

                    return (ix > Bits(1.0)) == (topy < 0x800u) ? double.PositiveInfinity : 0.0;
                }

                if (topx == 0)
                {
                    ix = Bits(x * 4503599627370496.0) & 0x7fffffffffffffffUL; // x * 0x1p52
                    ix -= 52UL << 52;
                }
            }
        }

        double hi = Log(ix, out double lo);
        double ehi = y * hi;
        double elo = Fma(y, lo, Fma(y, hi, -ehi));
        return Exp(ehi, elo);
    }

    private static double Log(ulong ix, out double tail)
    {
        unchecked
        {
            ulong tmp = ix - Off;
            int i = (int)((tmp >> 45) % 128);
            long k = (long)tmp >> 52;
            ulong iz = ix - (tmp & (0xfffUL << 52));
            double z = Dbl(iz), kd = k;
            double invc = AorPowLogTable.InvC[i], logc = AorPowLogTable.LogC[i], logctail = AorPowLogTable.LogCTail[i];
            var a = AorPowLogTable.Poly;
            double r = Fma(z, invc, -1.0);
            double t1 = Fma(kd, AorPowLogTable.Ln2Hi, logc);
            double t2 = t1 + r;
            double lo1 = Fma(kd, AorPowLogTable.Ln2Lo, logctail);
            double lo2 = t1 - t2 + r;
            double ar = a[0] * r, ar2 = r * ar, ar3 = r * ar2;
            double hi = t2 + ar2;
            double lo3 = Fma(ar, r, -ar2);
            double lo4 = t2 - hi + ar2;
            double q = Fma(ar2, Fma(ar2, Fma(r, a[6], a[5]), Fma(r, a[4], a[3])), Fma(r, a[2], a[1]));
            double lo = Fma(ar3, q, lo1 + lo2 + lo3 + lo4);
            double y = hi + lo;
            tail = hi - y + lo;
            return y;
        }
    }

    private static double Exp(double x, double xtail)
    {
        unchecked
        {
            uint abstop = (uint)(Bits(x) >> 52) & 0x7ffu;
            if (abstop - 0x3c9u >= 0x408u - 0x3c9u)
            {
                if (abstop - 0x3c9u >= 0x80000000u)
                {
                    return 1.0 + x;
                }

                if (abstop >= 0x409u)
                {
                    return (Bits(x) >> 63) != 0 ? 0.0 : double.PositiveInfinity;
                }

                abstop = 0;
            }

            double kd = Fma(AorExpTable.InvLn2N, x, AorExpTable.Shift);
            ulong ki = Bits(kd);
            kd -= AorExpTable.Shift;
            double r = Fma(kd, AorExpTable.NegLn2LoN, Fma(kd, AorExpTable.NegLn2HiN, x));
            r += xtail;
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
                double y = Fma(scale, tmp, scale);
                return y * Dbl(0x7F00000000000000UL); // 0x1p1009
            }

            sbits += 1022UL << 52;
            double sc = Dbl(sbits);
            double st = sc * tmp;
            double yy = sc + st;
            if (Math.Abs(yy) < 1.0)
            {
                double one = yy < 0.0 ? -1.0 : 1.0;
                double lo = sc - yy + st;
                double hi = one + yy;
                lo = one - hi + yy + lo;
                yy = (hi + lo) - one;
                if (yy == 0.0)
                {
                    yy = Dbl(sbits & 0x8000000000000000UL);
                }
            }

            return Dbl(0x0010000000000000UL) * yy; // 0x1p-1022
        }
    }

    private static double Fma(double a, double b, double c) => Math.FusedMultiplyAdd(a, b, c);

    private static ulong Bits(double value) => BitConverter.DoubleToUInt64Bits(value);

    private static double Dbl(ulong bits) => BitConverter.UInt64BitsToDouble(bits);
}
