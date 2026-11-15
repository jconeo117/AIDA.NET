using System.Text.RegularExpressions;

namespace Aida.Platform.Identity;

/// <summary>La forma del usuario, del nombre y de la contraseña.</summary>
/// <remarks>
/// El usuario se guarda en minúsculas y solo con letras ASCII, cifras, punto, guion y subrayado: dos usuarios que se
/// parecen a la vista no pueden ser distintos. La contraseña tiene de 10 a 128 caracteres y ninguna regla de
/// composición (ADR 0030). El nombre se limpia de espacios repetidos, como <c>identity.py:118</c> de la 0.1.
/// </remarks>
internal static partial class CredentialRules
{
    internal const int MinPasswordLength = 10;
    internal const int MaxPasswordLength = 128;

    internal static string UserName(string userName)
    {
        var normalized = userName.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            throw new IdentityException(IdentityError.Missing);
        }

        return UserNamePattern().IsMatch(normalized) ? normalized : throw new IdentityException(IdentityError.UserNameFormat);
    }

    internal static string DisplayName(string displayName)
    {
        var normalized = string.Join(' ', displayName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length is > 0 and <= 80 ? normalized : throw new IdentityException(IdentityError.Missing);
    }

    internal static void Password(string password)
    {
        if (password.Length is < MinPasswordLength or > MaxPasswordLength)
        {
            throw new IdentityException(IdentityError.PasswordFormat);
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{2,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex UserNamePattern();
}
