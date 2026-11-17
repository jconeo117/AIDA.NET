using System.Security.Cryptography;
using System.Text;
using Aida.Platform.Keys;

namespace Aida.Platform.Sealing;

/// <summary>Sella y abre texto con AES-GCM y una clave derivada de la del equipo.</summary>
/// <remarks>
/// <para>Lo que identifica a una persona atendida no se guarda en claro (ADR 0009, ADR 0029). AES-GCM autentica además de cifrar:
/// un byte cambiado o una clave ajena no dan otro texto, dan un error.</para>
/// <para>El formato es una versión (<c>1</c>), el nonce aleatorio de 12 bytes, el cifrado y la etiqueta de 16. La
/// versión permite cambiar de algoritmo sin confundir lo viejo con lo nuevo. No hay compatibilidad con Fernet, de la 0.1
/// (ADR 0009).</para>
/// </remarks>
public sealed class Sealer
{
    /// <summary>El propósito de la clave derivada que sella.</summary>
    public const string Purpose = "aida-seal";

    private const byte _version = 1;
    private const int _nonceLength = 12;
    private const int _tagLength = 16;

    private readonly byte[] _key;

    /// <summary>Crea el sellador con la clave del equipo.</summary>
    /// <param name="machineKey">La clave del equipo; se usa la derivada de <see cref="Purpose"/>.</param>
    public Sealer(MachineKey machineKey)
    {
        ArgumentNullException.ThrowIfNull(machineKey);
        _key = machineKey.Derive(Purpose);
    }

    /// <summary>Sella un texto.</summary>
    /// <param name="plaintext">El texto, que puede estar vacío.</param>
    /// <returns>Versión, nonce, cifrado y etiqueta. Dos sellados del mismo texto no se parecen.</returns>
    public byte[] Seal(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var clear = Encoding.UTF8.GetBytes(plaintext);
        var sealedValue = new byte[1 + _nonceLength + clear.Length + _tagLength];
        sealedValue[0] = _version;
        var nonce = sealedValue.AsSpan(1, _nonceLength);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, _tagLength);
        aes.Encrypt(nonce, clear, sealedValue.AsSpan(1 + _nonceLength, clear.Length), sealedValue.AsSpan(1 + _nonceLength + clear.Length));
        return sealedValue;
    }

    /// <summary>Abre lo sellado.</summary>
    /// <param name="sealedValue">Lo que devolvió <see cref="Seal"/>.</param>
    /// <returns>El texto.</returns>
    /// <exception cref="InvalidDataException">Si no tiene el formato, si es de otra versión, o si la etiqueta no cuadra (otra
    /// clave, o datos alterados).</exception>
    public string Open(byte[] sealedValue)
    {
        ArgumentNullException.ThrowIfNull(sealedValue);
        if (sealedValue.Length < 1 + _nonceLength + _tagLength || sealedValue[0] != _version)
        {
            throw new InvalidDataException("lo sellado no tiene el formato de la versión 1");
        }

        var length = sealedValue.Length - 1 - _nonceLength - _tagLength;
        var clear = new byte[length];
        try
        {
            using var aes = new AesGcm(_key, _tagLength);
            aes.Decrypt(sealedValue.AsSpan(1, _nonceLength), sealedValue.AsSpan(1 + _nonceLength, length), sealedValue.AsSpan(1 + _nonceLength + length), clear);
        }
        catch (AuthenticationTagMismatchException)
        {
            throw new InvalidDataException("lo sellado no se abre: otra clave, o datos alterados");
        }

        return Encoding.UTF8.GetString(clear);
    }
}
