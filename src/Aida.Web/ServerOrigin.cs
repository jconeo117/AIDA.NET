using System.Net;

namespace Aida.Web;

/// <summary>Si una petición viene del propio servidor.</summary>
/// <remarks>El primer administrador solo se crea desde el equipo donde está instalada AIDA: si se pudiera desde la red,
/// el primero en llegar se quedaría con todo (ADR 0030). Una dirección IPv4 mapeada en IPv6 cuenta como la que es.</remarks>
internal static class ServerOrigin
{
    /// <summary>Si la petición llega desde el bucle local.</summary>
    /// <param name="http">La petición.</param>
    /// <returns><see langword="true"/> si viene del propio equipo.</returns>
    internal static bool IsServerItself(HttpContext http) =>
        http.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address);
}
