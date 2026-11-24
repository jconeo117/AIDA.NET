namespace Aida.Platform.Snapshots;

/// <summary>Por qué no se pudo hacer o aplicar una copia.</summary>
/// <remarks>
/// Cada motivo tiene su código en la URL y su texto en <c>ui.yaml</c> (<c>snapshot_error_&lt;código&gt;</c>), como
/// <c>reason</c> de la 0.1 (<c>snapshot.py:37-38</c>): el código no escribe mensajes para la pantalla.
/// </remarks>
public enum SnapshotError
{
    /// <summary>La frase tiene menos de 12 caracteres.</summary>
    Weak,

    /// <summary>La copia pasa del tope de 1 GiB de contenido.</summary>
    TooLarge,

    /// <summary>No es una copia de AIDA, o está dañada.</summary>
    BadFile,

    /// <summary>La frase no es la de esta copia.</summary>
    BadPassphrase,

    /// <summary>Trae una entrada que no está en la lista cerrada, o una repetida.</summary>
    UnsafePath,

    /// <summary>Es de un AIDA más nuevo que este, en la plataforma o en un brazo.</summary>
    TooNew,
}
