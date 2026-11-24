using Aida.Arms.Agenda.Storage;
using Aida.Platform.Audit;

namespace Aida.Arms.Agenda;

/// <summary>Lo que la agenda declara a la plataforma: su identificador, sus permisos y su clase de hecho.</summary>
/// <remarks>La plataforma no nombra a ningún brazo (ADR 0035): la composición le pasa estas constantes, y
/// <c>RoleCatalog</c> y <c>AuditLog</c> validan contra ellas. Quitar la agenda es quitarlas de la composición.</remarks>
public static class AgendaDeclarations
{
    /// <summary>El identificador del brazo: su carpeta de packs, el dueño de sus eventos y el nombre de su esquema.</summary>
    public const string ArmId = "agenda";

    /// <summary>Agendar, editar, reprogramar, cancelar y marcar citas. Ver el día no pide permiso.</summary>
    public const string ManagePermission = "agenda.manage";

    /// <summary>Bloquear y desbloquear tramos de la agenda.</summary>
    /// <remarks>Aparte de <see cref="ManagePermission"/> porque un bloqueo cambia la agenda de todos, no una cita: por
    /// defecto lo tienen administración y recepción, y el profesional no (debate de N009).</remarks>
    public const string BlockPermission = "agenda.block";

    /// <summary>Los permisos del brazo.</summary>
    public static IReadOnlyList<string> Permissions { get; } = [ManagePermission, BlockPermission];

    /// <summary>Una cita cambió por la puerta (<c>agenda_changed</c>, como en la 0.1, <c>audit.py:45</c>).</summary>
    public static AuditKind Changed { get; } = AuditKind.Declare("agenda_changed");

    /// <summary>Las clases de hecho del brazo.</summary>
    public static IReadOnlyList<AuditKind> AuditKinds { get; } = [Changed];

    /// <summary>La versión de esquema que conoce este código; la lee la restauración para no aceptar una copia más nueva.</summary>
    public static int SchemaVersion => AgendaSchema.Steps.Count;
}
