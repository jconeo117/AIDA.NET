using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Aida.Platform.Snapshots;

/// <summary>Las constantes y las piezas del formato <c>.aidacopia</c>.</summary>
/// <remarks>
/// <para>Una cabecera y trozos de 1 MiB, cada uno con su AES-GCM. El número del trozo va en el nonce y en los datos
/// asociados, junto con la cabecera y la marca de último: un trozo cambiado de sitio, uno quitado del final o una
/// cabecera retocada no verifican (ADR 0036).</para>
/// <para>La 0.1 usaba scrypt y Fernet sobre el archivo entero (<c>snapshot.py:51-53</c>). La biblioteca base no trae
/// scrypt, y la base no cabe entera en memoria: PBKDF2 con las iteraciones de las contraseñas (ADR 0030) y trozos.</para>
/// </remarks>
internal static class SnapshotFormat
{
    internal const int SaltLength = 16;
    internal const int HeaderLength = 11 + SaltLength + 4;
    internal const int Iterations = 600_000;
    internal const int MaxIterations = 10_000_000;
    internal const int ChunkSize = 1 << 20;
    internal const int TagLength = 16;
    internal const long MaxBytes = 1L << 30;
    internal const int MinPassphrase = 12;
    internal const byte MiddleChunk = 0x00;
    internal const byte LastChunk = 0x01;

    internal static ReadOnlySpan<byte> Magic => "AIDACOPIA2\n"u8;

    internal static byte[] DeriveKey(string passphrase, ReadOnlySpan<byte> salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(passphrase), salt, iterations, HashAlgorithmName.SHA256, 32);

    internal static void Nonce(long index, Span<byte> nonce)
    {
        nonce[..4].Clear();
        BinaryPrimitives.WriteInt64BigEndian(nonce[4..], index);
    }

    internal static byte[] AssociatedData(ReadOnlySpan<byte> header, long index, byte flag)
    {
        var aad = new byte[HeaderLength + 9];
        header.CopyTo(aad);
        BinaryPrimitives.WriteInt64BigEndian(aad.AsSpan(HeaderLength), index);
        aad[HeaderLength + 8] = flag;
        return aad;
    }
}
