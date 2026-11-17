namespace Aida.Platform.Keys;

/// <summary>Elige cómo se protege la clave del equipo en este servidor.</summary>
/// <remarks>Una sola puerta para la elección (ADR 0034): en Windows, DPAPI; fuera de Windows, solo en desarrollo, nada.
/// Un servidor de producción fuera de Windows no deja la clave en claro.</remarks>
public static class KeyProtectors
{
    /// <summary>El protector de este servidor.</summary>
    /// <param name="isDevelopment">Si el entorno es el de desarrollo.</param>
    /// <returns>DPAPI en Windows; el de desarrollo fuera de Windows en desarrollo.</returns>
    /// <exception cref="PlatformNotSupportedException">Fuera de Windows y fuera de desarrollo.</exception>
    public static IKeyProtector ForHost(bool isDevelopment)
    {
        if (OperatingSystem.IsWindows())
        {
            return new DpapiKeyProtector();
        }

        return isDevelopment
            ? new DevelopmentKeyProtector()
            : throw new PlatformNotSupportedException("la clave del equipo solo se protege con DPAPI, en Windows; fuera de Windows solo se admite el entorno de desarrollo");
    }
}
