using System.Buffers.Binary;
using System.Numerics;

namespace Aida.Engine.Hashing;

/// <summary>BLAKE2b sin clave, según el RFC 7693.</summary>
/// <remarks>Propio y no de un paquete (ADR 0023). No cifra ni firma: da huellas de integridad y, más
/// adelante, siembra los sorteos. Se comprueba con los vectores del RFC y con los de la 0.1.</remarks>
internal static class Blake2b
{
    private const int _blockSize = 128;
    private const int _maxDigestSize = 64;
    private const int _rounds = 12;

    // Las constantes van en propiedades y no en arrays estáticos: el guardián C4 marca los arrays estáticos.
    private static ReadOnlySpan<ulong> Iv => [
        0x6a09e667f3bcc908UL, 0xbb67ae8584caa73bUL, 0x3c6ef372fe94f82bUL, 0xa54ff53a5f1d36f1UL,
        0x510e527fade682d1UL, 0x9b05688c2b3e6c1fUL, 0x1f83d9abfb41bd6bUL, 0x5be0cd19137e2179UL,
    ];

    private static ReadOnlySpan<byte> Sigma => [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
        14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3,
        11, 8, 12, 0, 5, 2, 15, 13, 10, 14, 3, 6, 7, 1, 9, 4,
        7, 9, 3, 1, 13, 12, 11, 14, 2, 6, 5, 10, 4, 0, 15, 8,
        9, 0, 5, 7, 2, 4, 10, 15, 14, 1, 11, 12, 6, 8, 3, 13,
        2, 12, 6, 10, 0, 11, 8, 3, 4, 13, 7, 5, 15, 14, 1, 9,
        12, 5, 1, 15, 14, 13, 4, 10, 0, 7, 6, 3, 9, 2, 8, 11,
        13, 11, 7, 14, 12, 1, 3, 9, 5, 0, 15, 4, 8, 6, 2, 10,
        6, 15, 14, 9, 11, 3, 0, 8, 12, 2, 13, 7, 1, 4, 10, 5,
        10, 2, 8, 4, 7, 6, 1, 5, 15, 11, 9, 14, 3, 12, 13, 0,
    ];

    /// <summary>La huella de unos bytes.</summary>
    /// <param name="input">Los bytes.</param>
    /// <param name="digestSize">Bytes de huella, de 1 a 64.</param>
    /// <returns>La huella, de <paramref name="digestSize"/> bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="digestSize"/> no está entre 1 y 64.</exception>
    internal static byte[] Hash(ReadOnlySpan<byte> input, int digestSize)
    {
        if (digestSize < 1 || digestSize > _maxDigestSize)
        {
            throw new ArgumentOutOfRangeException(nameof(digestSize), digestSize, "La huella tiene que medir entre 1 y 64 bytes.");
        }

        Span<ulong> state = stackalloc ulong[8];
        Iv.CopyTo(state);
        state[0] ^= 0x01010000UL ^ (ulong)digestSize;

        var counter = 0UL;
        var offset = 0;

        // El último bloque, aunque esté lleno, es el que lleva la marca de final: por eso el bucle deja uno.
        while (input.Length - offset > _blockSize)
        {
            counter += _blockSize;
            Compress(state, input.Slice(offset, _blockSize), counter, last: false);
            offset += _blockSize;
        }

        var remaining = input.Length - offset;
        Span<byte> lastBlock = stackalloc byte[_blockSize];
        input[offset..].CopyTo(lastBlock);
        counter += (ulong)remaining;
        Compress(state, lastBlock, counter, last: true);

        Span<byte> full = stackalloc byte[_maxDigestSize];
        for (var i = 0; i < state.Length; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(full[(i * 8)..], state[i]);
        }

        return full[..digestSize].ToArray();
    }

    private static void Compress(Span<ulong> state, ReadOnlySpan<byte> block, ulong counter, bool last)
    {
        Span<ulong> m = stackalloc ulong[16];
        for (var i = 0; i < m.Length; i++)
        {
            m[i] = BinaryPrimitives.ReadUInt64LittleEndian(block[(i * 8)..]);
        }

        Span<ulong> v = stackalloc ulong[16];
        state.CopyTo(v);
        Iv.CopyTo(v[8..]);
        v[12] ^= counter;
        if (last)
        {
            v[14] = ~v[14];
        }

        var sigma = Sigma;
        for (var round = 0; round < _rounds; round++)
        {
            var s = (round % 10) * 16;
            Mix(v, 0, 4, 8, 12, m[sigma[s]], m[sigma[s + 1]]);
            Mix(v, 1, 5, 9, 13, m[sigma[s + 2]], m[sigma[s + 3]]);
            Mix(v, 2, 6, 10, 14, m[sigma[s + 4]], m[sigma[s + 5]]);
            Mix(v, 3, 7, 11, 15, m[sigma[s + 6]], m[sigma[s + 7]]);
            Mix(v, 0, 5, 10, 15, m[sigma[s + 8]], m[sigma[s + 9]]);
            Mix(v, 1, 6, 11, 12, m[sigma[s + 10]], m[sigma[s + 11]]);
            Mix(v, 2, 7, 8, 13, m[sigma[s + 12]], m[sigma[s + 13]]);
            Mix(v, 3, 4, 9, 14, m[sigma[s + 14]], m[sigma[s + 15]]);
        }

        for (var i = 0; i < 8; i++)
        {
            state[i] ^= v[i] ^ v[i + 8];
        }
    }

    // La función G del RFC 7693: rotaciones 32, 24, 16 y 63.
    private static void Mix(Span<ulong> v, int a, int b, int c, int d, ulong x, ulong y)
    {
        v[a] = v[a] + v[b] + x;
        v[d] = BitOperations.RotateRight(v[d] ^ v[a], 32);
        v[c] += v[d];
        v[b] = BitOperations.RotateRight(v[b] ^ v[c], 24);
        v[a] = v[a] + v[b] + y;
        v[d] = BitOperations.RotateRight(v[d] ^ v[a], 16);
        v[c] += v[d];
        v[b] = BitOperations.RotateRight(v[b] ^ v[c], 63);
    }
}
