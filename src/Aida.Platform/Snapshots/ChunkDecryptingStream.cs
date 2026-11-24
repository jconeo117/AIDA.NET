using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Aida.Platform.Snapshots;

/// <summary>Descifra y verifica la copia por trozos mientras se lee.</summary>
/// <remarks>
/// <para>Cada error de la copia sale como <see cref="SnapshotException"/> con su motivo (ADR 0036). Un trozo que no
/// verifica en el primer lugar es la frase equivocada; en cualquier otro, la copia está manipulada o dañada. Por eso
/// los dos casos tienen motivos distintos: la pantalla pide la frase de nuevo solo cuando de verdad puede servir.</para>
/// <para>Su lectura devuelve 0 solo después de verificar el trozo último y el fin del archivo: un byte de más
/// tras el último trozo no se acepta.</para>
/// </remarks>
internal sealed class ChunkDecryptingStream : Stream
{
    private readonly Stream _input;
    private readonly long _maxBytes;
    private readonly byte[] _header = new byte[SnapshotFormat.HeaderLength];
    private readonly AesGcm _aes;
    private byte[] _plain = [];
    private int _position;
    private long _index;
    private long _total;
    private bool _lastSeen;
    private bool _disposed;

    /// <summary>Lee la cabecera y deriva la clave.</summary>
    /// <param name="input">De dónde se lee. No se cierra.</param>
    /// <param name="passphrase">La frase de la copia.</param>
    /// <param name="maxBytes">El tope de contenido en claro.</param>
    /// <exception cref="SnapshotException"><see cref="SnapshotError.BadFile"/> si la cabecera es corta, la magia no
    /// coincide o las iteraciones están fuera de su rango. Ese rango evita que un archivo fabricado pida 2³² iteraciones.</exception>
    internal ChunkDecryptingStream(Stream input, string passphrase, long maxBytes = SnapshotFormat.MaxBytes)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(passphrase);
        _input = input;
        _maxBytes = maxBytes;
        ReadExactlyOrBadFile(_header);
        if (!_header.AsSpan(0, SnapshotFormat.Magic.Length).SequenceEqual(SnapshotFormat.Magic))
        {
            throw new SnapshotException(SnapshotError.BadFile);
        }

        var salt = _header.AsSpan(SnapshotFormat.Magic.Length, SnapshotFormat.SaltLength);
        var iterations = BinaryPrimitives.ReadUInt32BigEndian(_header.AsSpan(SnapshotFormat.HeaderLength - 4));
        if (iterations < SnapshotFormat.Iterations || iterations > SnapshotFormat.MaxIterations)
        {
            throw new SnapshotException(SnapshotError.BadFile);
        }

        var key = SnapshotFormat.DeriveKey(passphrase, salt, (int)iterations);
        try
        {
            _aes = new AesGcm(key, SnapshotFormat.TagLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    /// <exception cref="SnapshotException">El motivo del error de la copia, según la tabla de ADR 0036.</exception>
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffer.IsEmpty)
        {
            return 0;
        }

        while (_position == _plain.Length)
        {
            if (_lastSeen)
            {
                EnsureEndOfInput();
                return 0;
            }

            ReadChunk();
        }

        var count = Math.Min(buffer.Length, _plain.Length - _position);
        _plain.AsSpan(_position, count).CopyTo(buffer);
        _position += count;
        return count;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _aes.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ReadChunk()
    {
        Span<byte> prefix = stackalloc byte[5];
        ReadExactlyOrBadFile(prefix);
        var flag = prefix[0];
        var length = BinaryPrimitives.ReadUInt32BigEndian(prefix[1..]);
        if (flag is not (SnapshotFormat.MiddleChunk or SnapshotFormat.LastChunk))
        {
            throw new SnapshotException(SnapshotError.BadFile);
        }

        var valid = flag == SnapshotFormat.MiddleChunk
            ? length == SnapshotFormat.ChunkSize
            : length <= SnapshotFormat.ChunkSize;
        if (!valid)
        {
            throw new SnapshotException(SnapshotError.BadFile);
        }

        if (_total + length > _maxBytes)
        {
            throw new SnapshotException(SnapshotError.TooLarge);
        }

        var cipher = new byte[length];
        var tag = new byte[SnapshotFormat.TagLength];
        ReadExactlyOrBadFile(cipher);
        ReadExactlyOrBadFile(tag);
        var plain = new byte[length];
        Span<byte> nonce = stackalloc byte[12];
        SnapshotFormat.Nonce(_index, nonce);
        var aad = SnapshotFormat.AssociatedData(_header, _index, flag);
        try
        {
            _aes.Decrypt(nonce, cipher, tag, plain, aad);
        }
        catch (CryptographicException)
        {
            throw new SnapshotException(_index == 0 ? SnapshotError.BadPassphrase : SnapshotError.BadFile);
        }

        _plain = plain;
        _position = 0;
        _total += length;
        _index++;
        _lastSeen = flag == SnapshotFormat.LastChunk;
    }

    private void EnsureEndOfInput()
    {
        if (_input.ReadByte() != -1)
        {
            throw new SnapshotException(SnapshotError.BadFile);
        }
    }

    private void ReadExactlyOrBadFile(Span<byte> buffer)
    {
        try
        {
            _input.ReadExactly(buffer);
        }
        catch (EndOfStreamException exception)
        {
            throw new SnapshotException(SnapshotError.BadFile, exception);
        }
    }
}
