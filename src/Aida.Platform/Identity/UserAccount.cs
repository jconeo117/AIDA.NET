using System.Globalization;
using Aida.Platform.Audit;

namespace Aida.Platform.Identity;

/// <summary>Un usuario del servidor, sin su contraseña.</summary>
/// <remarks>La huella de la contraseña nunca sale de <see cref="UserStore"/>.</remarks>
/// <param name="Id">El identificador, que no cambia.</param>
/// <param name="UserName">Con qué entra: minúsculas, sin espacios.</param>
/// <param name="DisplayName">Cómo se muestra y cómo firma en la auditoría.</param>
/// <param name="RoleId">Su rol, declarado en <c>roles.yaml</c>.</param>
/// <param name="Active">Si puede entrar.</param>
public sealed record UserAccount(long Id, string UserName, string DisplayName, string RoleId, bool Active)
{
    /// <summary>El autor con el que firma sus hechos.</summary>
    public Actor Actor => new(Id.ToString(CultureInfo.InvariantCulture), DisplayName);
}
