namespace Aida.Platform.Keys;

/// <summary>No protege nada: guarda la clave tal cual. Solo para desarrollar fuera de Windows.</summary>
/// <remarks>Existe para poder probar la aplicación a mano en Linux, donde no hay DPAPI (ADR 0034). Solo lo elige
/// <see cref="KeyProtectors.ForHost"/> fuera de Windows y en el entorno de desarrollo; en producción fuera de Windows no
/// hay protector y la clave no se abre.</remarks>
public sealed class DevelopmentKeyProtector : IKeyProtector
{
    /// <inheritdoc/>
    public byte[] Protect(byte[] secret) => (byte[])secret.Clone();

    /// <inheritdoc/>
    public byte[] Unprotect(byte[] stored) => (byte[])stored.Clone();
}
