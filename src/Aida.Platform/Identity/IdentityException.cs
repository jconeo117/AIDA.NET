namespace Aida.Platform.Identity;

/// <summary>No se pudo hacer algo con un usuario.</summary>
/// <remarks>Lleva el <see cref="IdentityError"/> que elige el texto de la pantalla.</remarks>
public sealed class IdentityException : Exception
{
    /// <summary>Crea la excepción.</summary>
    /// <param name="error">El motivo.</param>
    public IdentityException(IdentityError error)
        : base($"identidad: {error}")
    {
        Error = error;
    }

    /// <summary>El motivo.</summary>
    public IdentityError Error { get; }
}
