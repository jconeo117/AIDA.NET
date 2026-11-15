namespace Aida.Platform.Sessions;

/// <summary>En qué está la sesión de un puesto.</summary>
/// <remarks>Los tres estados de <c>shell.py:204-262</c> de la 0.1, por puesto en vez de por equipo.</remarks>
public enum SessionStatus
{
    /// <summary>No hay sesión: hay que entrar.</summary>
    None,

    /// <summary>Hay alguien trabajando.</summary>
    Active,

    /// <summary>Se bloqueó por inactividad: vuelve la misma persona con su contraseña, o sale.</summary>
    Locked,
}
