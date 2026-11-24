using System.Globalization;
using System.Security.Claims;
using Aida.Platform.Identity;
using Aida.Platform.Sessions;
using Aida.Platform.Snapshots;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Aida.Web;

/// <summary>Abre la sesión del puesto y pone la cookie.</summary>
/// <remarks>Una sola puerta para entrar, la use la página de entrada o la del primer administrador: dos puertas
/// acabarían divergiendo (<c>shell.py:5-8</c> de la 0.1). La cookie solo lleva quién es y qué sesión; el estado vive en
/// <see cref="SessionTracker"/>.</remarks>
internal static class SignIn
{
    /// <summary>Abre la sesión y firma la cookie.</summary>
    /// <param name="http">La petición.</param>
    /// <param name="sessions">El registro de sesiones.</param>
    /// <param name="user">Quién entra, ya verificado.</param>
    /// <param name="notice">El aviso de una copia aplicada, si lo hay: lo firma el primero que entra.</param>
    /// <returns>La tarea de la cookie.</returns>
    internal static Task StartAsync(HttpContext http, SessionTracker sessions, UserAccount user, RestoreNotice notice)
    {
        var session = sessions.Open(user, AidaWebApp.Workstation(http));
        notice.SignIfPending(user.Actor, AidaWebApp.Workstation(http));
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(AidaWebApp.SessionClaim, session),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
