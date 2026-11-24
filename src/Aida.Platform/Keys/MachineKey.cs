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

        var created = RandomNumberGenerator.GetBytes(Length);
        WriteNew(filePath, created, protector);
        return new MachineKey(created);
    }

    /// <summary>Escribe una clave nueva en un archivo que no existe, ya envuelta con el protector.</summary>
    /// <param name="filePath">La ruta del archivo. Su carpeta se crea si no existe.</param>
    /// <param name="key">La clave en claro, de <see cref="Length"/> bytes.</param>
    /// <param name="protector">Cómo se protege en el disco.</param>
    /// <exception cref="IOException">Si el archivo ya existe: nunca se sobrescribe una clave.</exception>
    /// <remarks>
    /// Sin <c>CreateNew</c> una segunda escritura pisaría la clave y dejaría ilegible todo lo sellado. La restauración
    /// usa esta misma puerta para dejar la clave de la copia ya envuelta, sin que la clave en claro toque el disco (ADR 0036).
    /// </remarks>
    internal static void WriteNew(string filePath, byte[] key, IKeyProtector protector)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var stream = new FileStream(filePath, options))
        {
            stream.Write(protector.Protect(key));
        }
    }

    /// <summary>Una copia de la clave en claro, para la copia de seguridad.</summary>
    /// <returns>Los bytes de la clave; quien llama los borra al terminar.</returns>
    /// <remarks>Solo la copia cifrada la saca del equipo. Devolver una copia, y no la referencia, deja a salvo la clave de este objeto si quien llama la modifica.</remarks>
    internal byte[] Export() => (byte[])_key.Clone();

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
