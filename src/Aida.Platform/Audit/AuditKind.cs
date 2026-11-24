using System.Text.RegularExpressions;

namespace Aida.Platform.Audit;

/// <summary>Una clase de hecho de la auditoría, con el nombre que se escribe en la línea.</summary>
/// <remarks>
/// <para>Las siete de la plataforma son sus valores estáticos, con el mismo nombre que en la 0.1 (<c>audit.py:28-52</c>).
/// Las de un brazo las declara el brazo con <see cref="Declare"/>, y la composición se las pasa a <see cref="AuditLog"/>,
/// que rechaza las demás (ADR 0035). Así la plataforma no nombra ningún brazo y una clase que nadie declaró sigue sin
/// escribirse.</para>
/// <para>Era un <c>enum</c> en N007; el nombre en el disco no cambia, así que la cadena sigue verificando.</para>
/// </remarks>
public sealed partial record AuditKind
{
    private AuditKind(string name) => Name = name;

    /// <summary>Alguien entró.</summary>
    public static AuditKind SessionOpened { get; } = new("session_opened");

    /// <summary>Alguien salió.</summary>
    public static AuditKind SessionClosed { get; } = new("session_closed");

    /// <summary>El puesto se bloqueó por inactividad.</summary>
    public static AuditKind Locked { get; } = new("locked");

    /// <summary>Quien estaba volvió a entrar tras el bloqueo.</summary>
    public static AuditKind Unlocked { get; } = new("unlocked");

    /// <summary>Se creó un usuario.</summary>
    public static AuditKind UserCreated { get; } = new("user_created");

    /// <summary>Se desactivó un usuario.</summary>
    public static AuditKind UserDisabled { get; } = new("user_disabled");

    /// <summary>Un administrador puso una contraseña nueva a un usuario.</summary>
    public static AuditKind PasswordReset { get; } = new("password_reset");

    /// <summary>Se hizo una copia de la instalación, y quién la descargó.</summary>
    public static AuditKind SnapshotCreated { get; } = new("snapshot_created");

    /// <summary>Se aplicó una copia restaurada, firmada por quien entró primero después.</summary>
    public static AuditKind SnapshotRestored { get; } = new("snapshot_restored");

    /// <summary>Las clases de la plataforma: las de N007, y al final las de la copia de seguridad (N011).</summary>
    public static IReadOnlyList<AuditKind> Platform { get; } =
        [SessionOpened, SessionClosed, Locked, Unlocked, UserCreated, UserDisabled, PasswordReset, SnapshotCreated, SnapshotRestored];

    /// <summary>El nombre que se escribe en el campo <c>kind</c> de la línea.</summary>
    public string Name { get; }

    /// <summary>Declara la clase de hecho de un brazo.</summary>
    /// <param name="name">Su nombre en la línea (<c>^[a-z][a-z_]*$</c>).</param>
    /// <returns>La clase.</returns>
    /// <exception cref="ArgumentException">Si el nombre no cumple el patrón.</exception>
    public static AuditKind Declare(string name) =>
        name is not null && NamePattern().IsMatch(name)
            ? new AuditKind(name)
            : throw new ArgumentException($"«{name}» no es un nombre de clase de hecho válido", nameof(name));

    /// <inheritdoc/>
    public override string ToString() => Name;

    [GeneratedRegex("^[a-z][a-z_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
