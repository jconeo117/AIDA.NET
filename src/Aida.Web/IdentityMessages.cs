using System.Globalization;
using Aida.Platform.Identity;
using Aida.Platform.Wording;

namespace Aida.Web;

/// <summary>El texto de pantalla de un error de identidad.</summary>
/// <remarks>El código elige la clave <c>identity_&lt;código&gt;</c> de <c>ui.yaml</c>; el código no escribe mensajes
/// para la pantalla (<c>identity.py:35-42</c> de la 0.1).</remarks>
internal static class IdentityMessages
{
    /// <summary>El texto de un error.</summary>
    /// <param name="text">Los textos del armazón.</param>
    /// <param name="exception">El error.</param>
    /// <param name="attemptsLeft">Cuántos intentos quedan, para el mensaje de credenciales incorrectas.</param>
    /// <returns>El texto para la pantalla.</returns>
    internal static string For(ShellWording text, IdentityException exception, int attemptsLeft) => exception.Error switch
    {
        IdentityError.Missing => text["identity_missing"],
        IdentityError.UserNameFormat => text["identity_user_name_format"],
        IdentityError.PasswordFormat => text["identity_password_format"],
        IdentityError.Duplicate => text["identity_duplicate"],
        IdentityError.Unknown => text["identity_unknown"],
        IdentityError.BadCredentials => string.Format(CultureInfo.CurrentCulture, text["identity_bad_credentials"], attemptsLeft),
        IdentityError.Wait => text["identity_wait"],
        IdentityError.RoleUnknown => text["identity_role_unknown"],
        IdentityError.Forbidden => text["identity_forbidden"],
        IdentityError.LastAdministrator => text["identity_last_administrator"],
        IdentityError.AlreadyInitialized => text["identity_already_initialized"],
        _ => throw new ArgumentOutOfRangeException(nameof(exception), exception.Error, "error de identidad sin texto"),
    };
}
