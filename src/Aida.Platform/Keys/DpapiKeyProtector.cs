using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace Aida.Platform.Keys;

/// <summary>Protege la clave del equipo con DPAPI, ligada a la cuenta de Windows que ejecuta el servidor.</summary>
/// <remarks>Ámbito de usuario y no de máquina (ADR 0009, ADR 0034): otro usuario del mismo equipo no la abre. La entropía
/// fija separa esta clave de cualquier otro secreto que la misma cuenta proteja con DPAPI.</remarks>
[SupportedOSPlatform("windows")]
public sealed class DpapiKeyProtector : IKeyProtector
{
    private static ReadOnlySpan<byte> Entropy => "AIDA.MachineKey.v1"u8;

    /// <inheritdoc/>
    public byte[] Protect(byte[] secret) => ProtectedData.Protect(secret, Entropy.ToArray(), DataProtectionScope.CurrentUser);

    /// <inheritdoc/>
    public byte[] Unprotect(byte[] stored) => ProtectedData.Unprotect(stored, Entropy.ToArray(), DataProtectionScope.CurrentUser);
}
