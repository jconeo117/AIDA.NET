using System.Numerics;

namespace Aida.Engine.Randomness;

/// <summary>MT19937 sembrado como <c>random.Random(n)</c> de CPython 3.12, para un <c>n</c> de 64 bits.</summary>
/// <remarks>
/// <para>La 0.1 siembra un <c>random.Random</c> con la huella de 64 bits de cada clave (<c>rng.py:62</c>). Para
/// sortear lo mismo hay que reproducir su generador y su siembra: <c>random_seed</c> parte el entero en palabras de 32
/// bits, de menor a mayor, y usa tantas como hagan falta (al menos una), y <c>init_by_array</c> las mezcla sobre
/// <c>init_genrand(19650218)</c>. Una semilla que cabe en 32 bits usa una sola palabra, no dos.</para>
/// <para>No es <c>System.Random</c>, que está prohibido en el núcleo (ADR 0023) y además es otro algoritmo.</para>
/// </remarks>
internal sealed class MersenneTwister
{
    private const int N = 624;
    private const int M = 397;
    private readonly uint[] _mt = new uint[N];
    private int _index;

    private MersenneTwister()
    {
    }

    /// <summary>El generador de <c>random.Random(seed)</c>.</summary>
    /// <param name="seed">La semilla, como entero sin signo de 64 bits.</param>
    /// <returns>Un generador nuevo, en el mismo estado que el de Python.</returns>
    internal static MersenneTwister FromSeed(ulong seed)
    {
        int bits = 64 - BitOperations.LeadingZeroCount(seed);
        int keyUsed = bits == 0 ? 1 : ((bits - 1) / 32) + 1;
        Span<uint> key = [(uint)seed, (uint)(seed >> 32)];
        var generator = new MersenneTwister();
        generator.InitByArray(key[..keyUsed]);
        return generator;
    }

    /// <summary>Los siguientes 32 bits: <c>getrandbits(32)</c> de Python.</summary>
    /// <returns>Un entero sin signo de 32 bits.</returns>
    internal uint NextUInt32()
    {
        unchecked
        {
            if (_index >= N)
            {
                int kk;
                for (kk = 0; kk < N - M; kk++)
                {
                    uint y = (_mt[kk] & 0x80000000u) | (_mt[kk + 1] & 0x7fffffffu);
                    _mt[kk] = _mt[kk + M] ^ (y >> 1) ^ ((y & 1u) * 0x9908b0dfu);
                }

                for (; kk < N - 1; kk++)
                {
                    uint y = (_mt[kk] & 0x80000000u) | (_mt[kk + 1] & 0x7fffffffu);
                    _mt[kk] = _mt[kk + (M - N)] ^ (y >> 1) ^ ((y & 1u) * 0x9908b0dfu);
                }

                uint z = (_mt[N - 1] & 0x80000000u) | (_mt[0] & 0x7fffffffu);
                _mt[N - 1] = _mt[M - 1] ^ (z >> 1) ^ ((z & 1u) * 0x9908b0dfu);
                _index = 0;
            }

            uint t = _mt[_index++];
            t ^= t >> 11;
            t ^= (t << 7) & 0x9d2c5680u;
            t ^= (t << 15) & 0xefc60000u;
            t ^= t >> 18;
            return t;
        }
    }

    /// <summary>Un decimal en [0, 1) con 53 bits: <c>random()</c> de Python (<c>genrand_res53</c>).</summary>
    /// <returns>El siguiente decimal de la secuencia.</returns>
    internal double NextDouble()
    {
        uint a = NextUInt32() >> 5;
        uint b = NextUInt32() >> 6;
        return ((a * 67108864.0) + b) * (1.0 / 9007199254740992.0);
    }

    private void InitGenrand(uint s)
    {
        unchecked
        {
            _mt[0] = s;
            for (int i = 1; i < N; i++)
            {
                _mt[i] = (1812433253u * (_mt[i - 1] ^ (_mt[i - 1] >> 30))) + (uint)i;
            }

            _index = N;
        }
    }

    private void InitByArray(ReadOnlySpan<uint> key)
    {
        unchecked
        {
            InitGenrand(19650218u);
            int i = 1, j = 0;
            for (int k = Math.Max(N, key.Length); k > 0; k--)
            {
                _mt[i] = (_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1664525u)) + key[j] + (uint)j;
                i++;
                j++;
                if (i >= N)
                {
                    _mt[0] = _mt[N - 1];
                    i = 1;
                }

                if (j >= key.Length)
                {
                    j = 0;
                }
            }

            for (int k = N - 1; k > 0; k--)
            {
                _mt[i] = (_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1566083941u)) - (uint)i;
                i++;
                if (i >= N)
                {
                    _mt[0] = _mt[N - 1];
                    i = 1;
                }
            }

            _mt[0] = 0x80000000u;
        }
    }
}
