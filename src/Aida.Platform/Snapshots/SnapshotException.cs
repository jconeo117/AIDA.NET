namespace Aida.Platform.Snapshots;

/// <summary>Una copia que no se pudo escribir, validar o aplicar.</summary>
/// <remarks>
/// Lleva el <see cref="SnapshotError"/> que elige el texto de la pantalla y el código que va en la URL. Es el patrón de
/// <see cref="Aida.Platform.Identity.IdentityException"/>: el motivo viaja tal cual hasta la capa que muestra el mensaje.
/// </remarks>
public sealed class SnapshotException : Exception
{
    /// <summary>Crea la excepción.</summary>
    /// <param name="error">El motivo.</param>
    /// <param name="inner">La excepción que lo causó, si la hay.</param>
    public SnapshotException(SnapshotError error, Exception? inner = null)
        : base($"copia: {error}", inner)
    {
        Error = error;
    }

    /// <summary>El motivo.</summary>
    public SnapshotError Error { get; }

    /// <summary>El código del motivo, el que va en la URL y en la clave de texto.</summary>
    /// <remarks>Son los mismos códigos de la 0.1 (<c>snapshot.py:37-38</c>), en minúsculas y con guion bajo.</remarks>
    public string Code => Error switch
    {
        SnapshotError.Weak => "weak",
        SnapshotError.TooLarge => "too_large",
        SnapshotError.BadFile => "bad_file",
        SnapshotError.BadPassphrase => "bad_passphrase",
        SnapshotError.UnsafePath => "unsafe_path",
        SnapshotError.TooNew => "too_new",
        _ => throw new ArgumentOutOfRangeException(nameof(Error), Error, "motivo de copia sin código"),
    };
}
