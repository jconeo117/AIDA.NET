using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Aida.Platform.Identity;

/// <summary>La huella de una contraseña: PBKDF2-SHA256 con sal propia.</summary>
/// <remarks>
/// Como <c>identity.py:62-63</c> de la 0.1, de la biblioteca base. La contraseña no se guarda: se guarda
/// <c>pbkdf2-sha256$iteraciones$sal$huella</c>, y las iteraciones van en la huella para poder subirlas sin invalidar
/// las anteriores (ADR 0030). Los tests usan pocas iteraciones; el servidor, <see cref="ServerIterations"/>.
/// </remarks>
public sealed class PasswordHasher
{
    /// <summary>Las iteraciones del servidor, las que recomienda OWASP para PBKDF2-SHA256.</summary>
    public const int ServerIterations = 600_000;

    private const string _scheme = "pbkdf2-sha256";
    private const int _saltBytes = 16;
    private const int _hashBytes = 32;
    private readonly int _iterations;

    /// <summary>Crea el calculador.</summary>
    /// <param name="iterations">Las iteraciones de las huellas nuevas.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si no son positivas.</exception>
    public PasswordHasher(int iterations)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        _iterations = iterations;
    }

    /// <summary>La huella de una contraseña, con sal nueva.</summary>
    /// <param name="password">La contraseña.</param>
    /// <returns>La huella para guardar.</returns>
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(_saltBytes);
        var hash = Derive(password, salt, _iterations);
        return string.Join('$', _scheme, _iterations.ToString(CultureInfo.InvariantCulture), Convert.ToHexStringLower(salt), Convert.ToHexStringLower(hash));
    }

    /// <summary>Si la contraseña es la de la huella.</summary>
    /// <param name="password">La contraseña escrita.</param>
    /// <param name="stored">La huella guardada.</param>
    /// <returns><see langword="true"/> si coincide. Una huella ilegible no coincide nunca.</returns>
    public bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != _scheme
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromHexString(parts[2]);
            expected = Convert.FromHexString(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Derive(password, salt, iterations), expected);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, _hashBytes);
}
