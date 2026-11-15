using System.Security.Claims;
using Aida.Platform.Identity;
using Aida.Platform.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Endpoints;

namespace Aida.Web;

/// <summary>Pone cada petición frente a la sesión de su puesto: sin sesión, a entrar; bloqueada, a desbloquear.</summary>
/// <remarks>
/// La cookie dice qué sesión es; el estado lo dice <see cref="SessionTracker"/>, que puede haberla bloqueado o
/// cerrado. Solo cuentan como actividad las páginas y los formularios, nunca los archivos estáticos: si contaran, una
/// pestaña abierta impediría bloquear (<c>shell.py:238-243</c> de la 0.1). Sin ningún usuario, todo lleva a crear el
/// primero.
/// </remarks>
internal sealed class SessionGate
{
    private const string _userKey = "aida.user";
    private readonly RequestDelegate _next;

    /// <summary>Crea el filtro.</summary>
    /// <param name="next">El siguiente eslabón de la tubería.</param>
    public SessionGate(RequestDelegate next) => _next = next;

    /// <summary>El usuario que la sesión de esta petición identificó, si lo hay.</summary>
    /// <param name="http">La petición.</param>
    /// <returns>El usuario, o <see langword="null"/>.</returns>
    public static UserAccount? CurrentUser(HttpContext http) => http.Items[_userKey] as UserAccount;

    /// <summary>Decide si la petición sigue, se manda a entrar o a desbloquear.</summary>
    /// <param name="http">La petición.</param>
    /// <param name="sessions">El registro de sesiones.</param>
    /// <param name="users">Los usuarios, para saber si ya existe el primero.</param>
    /// <returns>La tarea de la petición.</returns>
    public async Task InvokeAsync(HttpContext http, SessionTracker sessions, UserStore users)
    {
        var page = http.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>() is not null;
        var path = http.Request.Path;
        if (http.User.FindFirstValue(AidaWebApp.SessionClaim) is { } session)
        {
            var view = sessions.Check(session);
            if (view.Status == SessionStatus.None)
            {
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                http.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
            else if (view.Status == SessionStatus.Locked && page && !path.StartsWithSegments("/bloqueado"))
            {
                http.Response.Redirect("/bloqueado");
                return;
            }
            else if (view.Status == SessionStatus.Active)
            {
                http.Items[_userKey] = view.User;
                if (page && path.StartsWithSegments("/bloqueado"))
                {
                    http.Response.Redirect("/");
                    return;
                }

                if (page || HttpMethods.IsPost(http.Request.Method))
                {
                    sessions.Touch(session);
                }
            }
        }
        else if (page && !path.StartsWithSegments("/primer-usuario") && !users.HasUsers())
        {
            http.Response.Redirect("/primer-usuario");
            return;
        }

        await _next(http);
    }
}
