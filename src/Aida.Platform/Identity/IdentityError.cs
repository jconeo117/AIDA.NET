namespace Aida.Platform.Identity;

/// <summary>Por qué no se pudo hacer algo con un usuario.</summary>
/// <remarks>El código elige el texto que ve quien está delante (<c>identity_&lt;código&gt;</c> en <c>ui.yaml</c>), como
/// <c>IdentityError.code</c> en la 0.1 (<c>identity.py:35-42</c>): el código no escribe mensajes para la pantalla.</remarks>
public enum IdentityError
{
    /// <summary>Falta el usuario o el nombre.</summary>
    Missing,

    /// <summary>El usuario no tiene la forma pedida.</summary>
    UserNameFormat,

    /// <summary>La contraseña es demasiado corta o demasiado larga.</summary>
    PasswordFormat,

    /// <summary>Ya hay un usuario con ese nombre.</summary>
    Duplicate,

    /// <summary>Ese usuario no existe.</summary>
    Unknown,

    /// <summary>El usuario o la contraseña no son correctos. No dice cuál de los dos.</summary>
    BadCredentials,

    /// <summary>Demasiados intentos: hay que esperar.</summary>
    Wait,

    /// <summary>El rol no está declarado en <c>roles.yaml</c>.</summary>
    RoleUnknown,

    /// <summary>Quien lo pide no tiene permiso para hacerlo.</summary>
    Forbidden,

    /// <summary>Es el último usuario activo que puede gestionar usuarios.</summary>
    LastAdministrator,

    /// <summary>Ya hay usuarios: el primer administrador solo se crea una vez.</summary>
    AlreadyInitialized,
}
