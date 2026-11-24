using System.Buffers.Binary;
using Aida.Platform.Snapshots;
using Xunit;

namespace Aida.Platform.Tests.Snapshots;

public sealed class ChunkStreamTests
{
    private const string Passphrase = "una_frase_segura_de_prueba_12";

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(SnapshotFormat.ChunkSize - 1)]
    [InlineData(SnapshotFormat.ChunkSize)]
    [InlineData(SnapshotFormat.ChunkSize + 1)]
    [InlineData(3 * SnapshotFormat.ChunkSize)]
    public void Ida_y_vuelta_con_diferentes_tamanos(int size)
    {
        var original = new byte[size];
        for (var i = 0; i < size; i++)
        {
            original[i] = (byte)(i & 0xFF);
        }

        using var memory = new MemoryStream();
        using (var writer = new ChunkEncryptingStream(memory, Passphrase))
        {
            writer.Write(original);
        }

        var encrypted = memory.ToArray();
        using var readStream = new MemoryStream(encrypted);
        using var reader = new ChunkDecryptingStream(readStream, Passphrase);
        var decrypted = new byte[size];
        var readTotal = 0;
        while (readTotal < size)
        {
            var read = reader.Read(decrypted.AsSpan(readTotal));
            if (read == 0)
            {
                break;
            }

            readTotal += read;
        }

        Assert.Equal(size, readTotal);
        Assert.Equal(0, reader.Read(new byte[1]));
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Con_tamano_exacto_de_trozo_hay_dos_trozos_y_el_ultimo_vacio()
    {
        var size = SnapshotFormat.ChunkSize;
        var data = new byte[size];
        using var memory = new MemoryStream();
        using (var writer = new ChunkEncryptingStream(memory, Passphrase))
        {
            writer.Write(data);
        }

        var bytes = memory.ToArray();
        // Cabecera: 31 bytes
        var offset = SnapshotFormat.HeaderLength;
        // Trozo 0: 1 (flag 0x00) + 4 (len = 1 MiB) + 1 MiB + 16 (tag)
        Assert.Equal(SnapshotFormat.MiddleChunk, bytes[offset]);
        var len0 = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 1, 4));
        Assert.Equal((uint)size, len0);
        offset += 1 + 4 + size + SnapshotFormat.TagLength;

        // Trozo 1: 1 (flag 0x01) + 4 (len = 0) + 0 + 16 (tag) = 21 bytes
        Assert.Equal(SnapshotFormat.LastChunk, bytes[offset]);
        var len1 = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 1, 4));
        Assert.Equal(0u, len1);
        Assert.Equal(bytes.Length, offset + 1 + 4 + 0 + SnapshotFormat.TagLength);
    }

    [Fact]
    public void Rechazo_por_otra_frase_es_BadPassphrase()
    {
        var bytes = Encrypt(new byte[100]);
        using var stream = new MemoryStream(bytes);
        using var reader = new ChunkDecryptingStream(stream, "otra_frase_distinta_12");
        var buffer = new byte[100];
        var ex = Assert.Throws<SnapshotException>(() => reader.Read(buffer));
        Assert.Equal(SnapshotError.BadPassphrase, ex.Error);
    }

    [Fact]
    public void Byte_cambiado_en_trozo_cero_es_BadPassphrase_y_en_trozo_uno_es_BadFile()
    {
        var data = new byte[SnapshotFormat.ChunkSize + 10];
        var bytes = Encrypt(data);

        // Trozo 0 ciphertext
        var corrupt0 = (byte[])bytes.Clone();
        corrupt0[SnapshotFormat.HeaderLength + 5 + 10] ^= 0xFF;
        using (var s0 = new MemoryStream(corrupt0))
        using (var r0 = new ChunkDecryptingStream(s0, Passphrase))
        {
            var buf = new byte[100];
            var ex0 = Assert.Throws<SnapshotException>(() => r0.ReadExactly(buf));
            Assert.Equal(SnapshotError.BadPassphrase, ex0.Error);
        }

        // Trozo 1 ciphertext
        var corrupt1 = (byte[])bytes.Clone();
        var chunk1CipherOffset = SnapshotFormat.HeaderLength + 1 + 4 + SnapshotFormat.ChunkSize + SnapshotFormat.TagLength + 5;
        corrupt1[chunk1CipherOffset] ^= 0xFF;
        using (var s1 = new MemoryStream(corrupt1))
        using (var r1 = new ChunkDecryptingStream(s1, Passphrase))
        {
            var buf = new byte[SnapshotFormat.ChunkSize + 10];
            var ex1 = Assert.Throws<SnapshotException>(() => r1.ReadExactly(buf));
            Assert.Equal(SnapshotError.BadFile, ex1.Error);
        }
    }

    [Fact]
    public void Marca_de_intermedio_a_ultimo_es_BadFile()
    {
        var data = new byte[2 * SnapshotFormat.ChunkSize + 10];
        var bytes = Encrypt(data);
        var chunk1Offset = SnapshotFormat.HeaderLength + 1 + 4 + SnapshotFormat.ChunkSize + SnapshotFormat.TagLength;
        bytes[chunk1Offset] = SnapshotFormat.LastChunk; // marca trozo 1 (intermedio) de 0x00 a 0x01
        using var stream = new MemoryStream(bytes);
        using var reader = new ChunkDecryptingStream(stream, Passphrase);
        var buf = new byte[data.Length];
        var ex = Assert.Throws<SnapshotException>(() =>
        {
            var read = 0;
            while (read < buf.Length)
            {
                var r = reader.Read(buf.AsSpan(read));
                if (r == 0) break;
                read += r;
            }
        });
        Assert.Equal(SnapshotError.BadFile, ex.Error);
    }

    [Fact]
    public void Quitar_el_trozo_ultimo_con_tamano_exacto_es_BadFile()
    {
        var data = new byte[SnapshotFormat.ChunkSize];
        var bytes = Encrypt(data);
        // El último trozo mide 21 bytes al final. Lo quitamos.
        var truncated = new byte[bytes.Length - 21];
        Array.Copy(bytes, truncated, truncated.Length);

        using var stream = new MemoryStream(truncated);
        using var reader = new ChunkDecryptingStream(stream, Passphrase);
        var buf = new byte[SnapshotFormat.ChunkSize];
        var ex = Assert.Throws<SnapshotException>(() =>
        {
            var read = 0;
            while (read < buf.Length)
            {
                read += reader.Read(buf.AsSpan(read));
            }

            reader.ReadExactly(new byte[1]); // intenta leer el siguiente trozo
        });
        Assert.Equal(SnapshotError.BadFile, ex.Error);
    }

    [Fact]
    public void Byte_de_mas_al_final_es_BadFile()
    {
        var bytes = Encrypt(new byte[50]);
        var withExtra = new byte[bytes.Length + 1];
        bytes.CopyTo(withExtra, 0);
        withExtra[^1] = 0x42;

        using var stream = new MemoryStream(withExtra);
        using var reader = new ChunkDecryptingStream(stream, Passphrase);
        var buf = new byte[50];
        reader.ReadExactly(buf);
        var ex = Assert.Throws<SnapshotException>(() => reader.ReadExactly(new byte[1]));
        Assert.Equal(SnapshotError.BadFile, ex.Error);
    }

    [Fact]
    public void Trozos_cambiados_de_sitio_es_BadFile()
    {
        var data = new byte[2 * SnapshotFormat.ChunkSize + 10];
        var bytes = Encrypt(data);

        // Header: 31
        // Chunk 0: 1 + 4 + 1MB + 16 = 1048597
        // Chunk 1: 1048597
        // Chunk 2: 1 + 4 + 10 + 16 = 31
        var c0Offset = SnapshotFormat.HeaderLength;
        var c0Len = 1 + 4 + SnapshotFormat.ChunkSize + SnapshotFormat.TagLength;
        var c1Offset = c0Offset + c0Len;
        var c1Len = c0Len;

        var swapped = (byte[])bytes.Clone();
        Array.Copy(bytes, c0Offset, swapped, c1Offset, c0Len);
        Array.Copy(bytes, c1Offset, swapped, c0Offset, c1Len);

        using var stream = new MemoryStream(swapped);
        using var reader = new ChunkDecryptingStream(stream, Passphrase);
        var buf = new byte[100];
        var ex = Assert.Throws<SnapshotException>(() => reader.Read(buf));
        Assert.Equal(SnapshotError.BadPassphrase, ex.Error); // Trozo 0 alterado falla como BadPassphrase
    }

    [Fact]
    public void Trozo_uno_y_dos_cambiados_de_sitio_es_BadFile()
    {
        var data = new byte[2 * SnapshotFormat.ChunkSize + 10];
        var bytes = Encrypt(data);

        var c0Offset = SnapshotFormat.HeaderLength;
        var c0Len = 1 + 4 + SnapshotFormat.ChunkSize + SnapshotFormat.TagLength;
        var c1Offset = c0Offset + c0Len;
        var c1Len = c0Len;
        var c2Offset = c1Offset + c1Len;
        var c2Len = bytes.Length - c2Offset;

        var swapped = (byte[])bytes.Clone();
        Array.Copy(bytes, c1Offset, swapped, c2Offset, c2Len < c1Len ? c2Len : c1Len);

        using var stream = new MemoryStream(swapped);
        using var reader = new ChunkDecryptingStream(stream, Passphrase);
        var buf = new byte[data.Length];
        var ex = Assert.Throws<SnapshotException>(() =>
        {
            var read = 0;
            while (read < buf.Length)
            {
                var r = reader.Read(buf.AsSpan(read));
                if (r == 0) break;
                read += r;
            }
        });
        Assert.Equal(SnapshotError.BadFile, ex.Error);
    }

    [Theory]
    [InlineData("AIDACOPIA1\n")]
    [InlineData("OTRAMAGIA0\n")]
    public void Otra_magia_es_BadFile(string badMagic)
    {
        var bytes = Encrypt(new byte[10]);
        var bad = (byte[])bytes.Clone();
        System.Text.Encoding.UTF8.GetBytes(badMagic).CopyTo(bad, 0);

        using var stream = new MemoryStream(bad);
        var ex = Assert.Throws<SnapshotException>(() => new ChunkDecryptingStream(stream, Passphrase));
        Assert.Equal(SnapshotError.BadFile, ex.Error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10_000_001)]
    public void Iteraciones_fuera_de_rango_es_BadFile(uint iters)
    {
        var bytes = Encrypt(new byte[10]);
        var bad = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(bad.AsSpan(SnapshotFormat.HeaderLength - 4), iters);

        using var stream = new MemoryStream(bad);
        var ex = Assert.Throws<SnapshotException>(() => new ChunkDecryptingStream(stream, Passphrase));
        Assert.Equal(SnapshotError.BadFile, ex.Error);
    }

    [Fact]
    public void Pasar_de_max_bytes_al_escribir_y_al_leer_es_TooLarge()
    {
        using var memW = new MemoryStream();
        using var writer = new ChunkEncryptingStream(memW, Passphrase, maxBytes: 100);
        var exW = Assert.Throws<SnapshotException>(() => writer.Write(new byte[101]));
        Assert.Equal(SnapshotError.TooLarge, exW.Error);

        var encrypted = Encrypt(new byte[100]);
        using var memR = new MemoryStream(encrypted);
        using var reader = new ChunkDecryptingStream(memR, Passphrase, maxBytes: 50);
        var buf = new byte[100];
        var exR = Assert.Throws<SnapshotException>(() => reader.Read(buf));
        Assert.Equal(SnapshotError.TooLarge, exR.Error);
    }

    private static byte[] Encrypt(byte[] data)
    {
        using var memory = new MemoryStream();
        using (var writer = new ChunkEncryptingStream(memory, Passphrase))
        {
            writer.Write(data);
        }

        return memory.ToArray();
    }
}
