using System.Collections.Frozen;
using System.Globalization;
using Aida.Platform.Packs;
using YamlDotNet.RepresentationModel;

namespace Aida.Platform.Wording;

/// <summary>Los textos del armazón y el tiempo sin uso tras el que se bloquea el puesto.</summary>
/// <remarks>
/// Es <c>platform/web/wording.py</c> de la 0.1: la sección <c>shell</c> de <c>packs/platform/ui.yaml</c>. Revienta al
/// cargar si falta una clave o el tiempo no es positivo: una pantalla a medias se descubre con alguien esperando
/// delante (<c>wording.py:72-74</c>). Solo se piden las claves que pinta alguna página; el archivo puede tener más.
/// </remarks>
public sealed class ShellWording
{
    /// <summary>Las claves que tiene que declarar la sección <c>shell</c>.</summary>
    public static IReadOnlyList<string> RequiredKeys { get; } =
    [
        "brand", "exit_label",
        "login_title", "login_lead", "user_name_label", "password_label", "enter_button",
        "first_title", "first_lead", "first_display_name", "first_password2", "first_button",
        "first_password_mismatch", "first_remote_only",
        "locked_title", "locked_lead", "unlock_button", "other_user",
        "users_title", "users_text", "users_add", "users_added", "users_role", "users_inactive",
        "users_disable", "users_disabled", "reset_button", "reset_done",
        "identity_missing", "identity_user_name_format", "identity_password_format", "identity_duplicate",
        "identity_unknown", "identity_bad_credentials", "identity_wait", "identity_role_unknown",
        "identity_forbidden", "identity_last_administrator", "identity_already_initialized",
        "settings_label", "title_settings", "settings_lead", "back_label",
        "snapshot_title", "snapshot_text", "snapshot_pass", "snapshot_pass2", "snapshot_create", "snapshot_first_link",
        "snapshot_restore_text", "snapshot_file", "snapshot_restore_pass", "snapshot_restore", "snapshot_restored",
        "snapshot_nofile", "snapshot_error_weak", "snapshot_error_mismatch", "snapshot_error_too_large",
        "snapshot_error_bad_file", "snapshot_error_bad_passphrase", "snapshot_error_unsafe_path", "snapshot_error_too_new",
    ];

    private readonly FrozenDictionary<string, string> _texts;

    private ShellWording(FrozenDictionary<string, string> texts, double idleLockMinutes)
    {
        _texts = texts;
        IdleLock = TimeSpan.FromMinutes(idleLockMinutes);
    }

    /// <summary>Tras cuánto tiempo sin uso se bloquea el puesto. Lo decide cada brazo en su pack.</summary>
    public TimeSpan IdleLock { get; }

    /// <summary>Un texto por su clave.</summary>
    /// <param name="key">La clave, sin la sección.</param>
    /// <returns>El texto.</returns>
    public string this[string key] => _texts[key];

    /// <summary>Lee la sección <c>shell</c> de <c>ui.yaml</c>.</summary>
    /// <param name="path">La ruta del archivo.</param>
    /// <returns>Los textos.</returns>
    /// <exception cref="InvalidDataException">Si falta la sección, alguna clave o el tiempo sin uso no es positivo.</exception>
    public static ShellWording Load(string path)
    {
        if (PlatformYaml.Child(PlatformYaml.ReadRoot(path), "shell") is not YamlMappingNode section)
        {
            throw new InvalidDataException($"falta la sección 'shell' en {path}");
        }

        var missing = RequiredKeys.Where(key => PlatformYaml.Text(PlatformYaml.Child(section, key)) is null).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidDataException($"faltan claves en la sección 'shell' de {path}: {string.Join(", ", missing)}");
        }

        var minutes = PlatformYaml.Child(section, "idle_lock_minutes") is YamlScalarNode { Style: YamlDotNet.Core.ScalarStyle.Plain } scalar
            && double.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 && double.IsFinite(parsed)
            ? parsed
            : throw new InvalidDataException($"'shell.idle_lock_minutes' debe ser un número positivo en {path}");
        var texts = RequiredKeys.ToFrozenDictionary(key => key, key => PlatformYaml.Text(PlatformYaml.Child(section, key))!, StringComparer.Ordinal);
        return new ShellWording(texts, minutes);
    }
}
