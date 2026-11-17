namespace Aida.Platform.Keys;

/// <summary>Protege la clave del equipo antes de escribirla en el disco, y la recupera al leerla.</summary>
/// <remarks>Una interfaz y no DPAPI directamente porque DPAPI solo existe en Windows y se desarrolla en Linux (ADR 0034).
/// Qué implementación se usa lo decide la composición con <see cref="KeyProtectors.ForHost"/>, nunca quien guarda datos.</remarks>
public interface IKeyProtector
{
    /// <summary>Protege la clave para guardarla.</summary>
    /// <param name="secret">La clave en claro.</param>
    /// <returns>Lo que se escribe en el archivo.</returns>
    byte[] Protect(byte[] secret);

    /// <summary>Recupera la clave de lo que se leyó del archivo.</summary>
    /// <param name="stored">Lo que había en el archivo.</param>
    /// <returns>La clave en claro.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">Si no se puede recuperar con esta protección.</exception>
    byte[] Unprotect(byte[] stored);
}
