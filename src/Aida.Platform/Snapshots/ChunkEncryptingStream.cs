using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Aida.Platform.Snapshots;

/// <summary>Cifra la copia por trozos mientras se escribe.</summary>
/// <remarks>
/// <para>Un trozo lleno no se emite hasta que llega el byte siguiente: así el último trozo siempre existe, y es el único
/// que lleva la marca de fin. Si el último se emitiera al llenarse, un archivo cortado justo en un límite de trozo
/// parecería completo (ADR 0036).</para>
/// <para>No cierra la salida: quien escribe la copia decide cuándo se cierra el archivo.</para>
/// </remarks>
internal sealed class ChunkEncryptingStream : Stream
{
    private readonly Stream _output;
    private readonly long _maxBytes;
    private readonly byte[] _header = new byte[SnapshotFormat.HeaderLength];
    private readonly byte[] _buffer = new byte[SnapshotFormat.ChunkSize];
    private readonly AesGcm _aes;
    private int _filled;
    private long _index;
    private long _total;
    private bool _disposed;

    /// <summary>Crea el cifrador y escribe la cabecera.</summary>
    /// <param name="output">Adónde se escribe. No se cierra.</param>
    /// <param name="passphrase">La frase, con la que se deriva la clave.</param>
    /// <param name="maxBytes">El tope de contenido en claro.</param>
    /// <remarks>La sal es aleatoria en cada copia: dos copias de lo mismo no se parecen, y la frase no se puede probar en lote.</remarks>
    internal ChunkEncryptingStream(Stream output, string passphrase, long maxBytes = SnapshotFormat.MaxBytes)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(passphrase);
        _output = output;
        _maxBytes = maxBytes;
        var salt = RandomNumberGenerator.GetBytes(SnapshotFormat.SaltLength);
        SnapshotFormat.Magic.CopyTo(_header);
        salt.CopyTo(_header.AsSpan(SnapshotFormat.Magic.Length));
        BinaryPrimitives.WriteUInt32BigEndian(
            _header.AsSpan(SnapshotFormat.Magic.Length + SnapshotFormat.SaltLength),
            (uint)SnapshotFormat.Iterations);
        var key = SnapshotFormat.DeriveKey(passphrase, salt, SnapshotFormat.Iterations);
        try
        {
            _aes = new AesGcm(key, SnapshotFormat.TagLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        _output.Write(_header);
    }

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush() => _output.Flush();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    /// <exception cref="SnapshotException"><see cref="SnapshotError.TooLarge"/> si el contenido pasa del tope.</exception>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_total + buffer.Length > _maxBytes)
        {
            throw new SnapshotException(SnapshotError.TooLarge);
        }

        while (!buffer.IsEmpty)
        {
            if (_filled == SnapshotFormat.ChunkSize)
            {
                EmitChunk(SnapshotFormat.MiddleChunk);
            }

            var take = Math.Min(SnapshotFormat.ChunkSize - _filled, buffer.Length);
            buffer[..take].CopyTo(_buffer.AsSpan(_filled));
            _filled += take;
            _total += take;
            buffer = buffer[take..];
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            try
            {
                if (_filled == SnapshotFormat.ChunkSize)
                {
                    EmitChunk(SnapshotFormat.MiddleChunk);
                    _filled = 0;
                }

                EmitChunk(SnapshotFormat.LastChunk);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(_buffer);
                _aes.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void EmitChunk(byte flag)
    {
        Span<byte> nonce = stackalloc byte[12];
        SnapshotFormat.Nonce(_index, nonce);
        var aad = SnapshotFormat.AssociatedData(_header, _index, flag);
        var cipher = new byte[_filled];
        var tag = new byte[SnapshotFormat.TagLength];
        _aes.Encrypt(nonce, _buffer.AsSpan(0, _filled), cipher, tag, aad);
        Span<byte> prefix = stackalloc byte[5];
        prefix[0] = flag;
        BinaryPrimitives.WriteUInt32BigEndian(prefix[1..], (uint)_filled);
        _output.Write(prefix);
        _output.Write(cipher);
        _output.Write(tag);
        _index++;
        _filled = 0;
    }
}
