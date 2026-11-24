using Aida.Platform.Wording;

namespace Aida.Web;

/// <summary>El texto de pantalla de una copia: de un código de la URL, a lo que se muestra.</summary>
/// <remarks>Solo los códigos conocidos llegan a la pantalla. Un código de la URL nunca se pinta tal cual: quien escribe la
/// URL podría meter cualquier texto en la página.</remarks>
internal static class SnapshotMessages
{
    /// <summary>El texto del código, o nada si no es uno conocido.</summary>
    /// <param name="text">Los textos del armazón.</param>
    /// <param name="code">El código de la URL; <see langword="null"/> si no viene.</param>
    /// <returns>El texto para la pantalla, o <see langword="null"/>.</returns>
    internal static string? For(ShellWording text, string? code) => code switch
    {
        "nofile" => text["snapshot_nofile"],
        "weak" or "mismatch" or "too_large" or "bad_file" or "bad_passphrase" or "unsafe_path" or "too_new"
            => text[$"snapshot_error_{code}"],
        _ => null,
    };
}
