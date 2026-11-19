using System.Security.Cryptography;
using System.Text;

namespace Aida.Platform.Keys;

/// <summary>La clave del equipo: 32 bytes que nunca salen del servidor, de los que se derivan las claves de uso.</summary>
/// <remarks>
/// <para>Se crea la primera vez y no se regenera nunca: regenerarla dejaría ilegible todo lo sellado. Si el archivo existe
/// y no se abre, se falla, no se crea otra (ADR 0034).</para>
/// <para>Nadie usa la clave directamente: cada uso deriva la suya con HKDF y un propósito, para que quien tenga la de una
/// huella no pueda abrir lo sellado (<c>store.py:150</c> de la 0.1 hacía lo mismo con BLAKE2b).</para>
/// </remarks>
public sealed class MachineKey
{
    /// <summary>El largo de la clave, en bytes.</summary>
    public const int Length = 32;

    private readonly byte[] _key;

    private MachineKey(byte[] key) => _key = key;

    /// <summary>Abre la clave del archivo, o la crea si el archivo no existe.</summary>
    /// <param name="filePath">La ruta del archivo. Su carpeta se crea si no existe.</param>
    /// <param name="protector">Cómo se protege en el disco.</param>
    /// <returns>La clave.</returns>
    /// <exception cref="InvalidDataException">Si el archivo existe y no se puede recuperar una clave de 32 bytes.</exception>
    public static MachineKey OpenOrCreate(string filePath, IKeyProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);
        if (File.Exists(filePath))
        {
            byte[] key;
            try
            {
                key = protector.Unprotect(File.ReadAllBytes(filePath));
            }
            catch (CryptographicException exception)
            {
                throw new InvalidDataException($"{filePath}: no se pudo abrir la clave del equipo: {exception.Message}");
            }

            return key.Length == Length
                ? new MachineKey(key)
                : throw new InvalidDataException($"{filePath}: la clave del equipo no tiene {Length} bytes");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        var created = RandomNumberGenerator.GetBytes(Length);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var stream = new FileStream(filePath, options))
        {
            stream.Write(protector.Protect(created));
        }

        return new MachineKey(created);
    }

    /// <summary>Deriva la clave de un uso.</summary>
    /// <param name="purpose">El propósito, en ASCII; por ejemplo, <see cref="Sealing.Sealer.Purpose"/>.</param>
    /// <returns>32 bytes, siempre los mismos para el mismo propósito.</returns>
    /// <exception cref="ArgumentException">Si el propósito está vacío o no es ASCII.</exception>
    public byte[] Derive(string purpose)
    {
        if (string.IsNullOrEmpty(purpose) || !Ascii.IsValid(purpose))
        {
            throw new ArgumentException("el propósito de una clave derivada es un texto ASCII no vacío", nameof(purpose));
        }

        return HKDF.DeriveKey(HashAlgorithmName.SHA256, _key, Length, salt: [], info: Encoding.ASCII.GetBytes(purpose));
    }
}
