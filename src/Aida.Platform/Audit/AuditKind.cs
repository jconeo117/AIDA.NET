namespace Aida.Platform.Audit;

/// <summary>Las clases de hecho que se registran.</summary>
/// <remarks>Cerradas, como <c>KINDS</c> en la 0.1 (<c>audit.py:27-51</c>): una clase que nadie conoce no se escribe.
/// Solo están las que escribe alguien hoy; cada brazo añade las suyas en su unidad.</remarks>
public enum AuditKind
{
    /// <summary>Alguien entró.</summary>
    SessionOpened,

    /// <summary>Alguien salió.</summary>
    SessionClosed,

    /// <summary>El puesto se bloqueó por inactividad.</summary>
    Locked,

    /// <summary>Quien estaba volvió a entrar tras el bloqueo.</summary>
    Unlocked,

    /// <summary>Se creó un usuario.</summary>
    UserCreated,

    /// <summary>Se desactivó un usuario.</summary>
    UserDisabled,

    /// <summary>Un administrador puso una contraseña nueva a un usuario.</summary>
    PasswordReset,
}
