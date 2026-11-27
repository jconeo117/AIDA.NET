using Aida.Platform.Audit;

namespace Aida.Arms.Health;

/// <summary>Lo que la historia clínica declara a la plataforma: su identificador, sus permisos y su clase de hecho.</summary>
/// <remarks>
/// <para>La plataforma no nombra a ningún brazo (ADR 0035): la composición le pasa estas constantes, y <c>RoleCatalog</c> y
/// <c>AuditLog</c> validan contra ellas. Quitar el brazo es quitarlas de la composición, sin tocar la plataforma.</para>
/// <para>Leer, redactar y firmar son permisos separados: un profesional puede tener los tres, pero la recepción puede
/// agendar sin leer ninguna historia. Así cada acción pide solo lo que hace.</para>
/// </remarks>
public static class HealthDeclarations
{
    /// <summary>El identificador del brazo: su carpeta de packs y el dueño de sus eventos.</summary>
    public const string ArmId = "health";

    /// <summary>Leer las atenciones de una persona y abrir una.</summary>
    public const string ReadPermission = "health.read";

    /// <summary>Crear una atención y guardar sus campos mientras es borrador.</summary>
    public const string RecordPermission = "health.record";

    /// <summary>Firmar una atención. Firmar es irreversible: además de este permiso, pide la contraseña de quien firma.</summary>
    public const string SignPermission = "health.sign";

    /// <summary>Los permisos del brazo.</summary>
    public static IReadOnlyList<string> Permissions { get; } = [ReadPermission, RecordPermission, SignPermission];

    /// <summary>La clase de documento que el brazo pone en la plataforma.</summary>
    /// <remarks>Es lo que separa una atención de otro documento de la plataforma: la lista de personas y el firmado la filtran por ella.</remarks>
    public const string RecordKind = "health_encounter";

    /// <summary>Una atención cambió por la puerta (<c>health_changed</c>). Lleva la acción, la atención y la persona, nunca el contenido.</summary>
    public static AuditKind Changed { get; } = AuditKind.Declare("health_changed");

    /// <summary>Las clases de hecho del brazo.</summary>
    public static IReadOnlyList<AuditKind> AuditKinds { get; } = [Changed];
}
