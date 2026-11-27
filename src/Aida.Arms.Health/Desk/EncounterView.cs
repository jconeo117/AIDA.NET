using Aida.Arms.Health.Setup;
using Aida.Platform.Records;

namespace Aida.Arms.Health.Desk;

/// <summary>Una atención abierta: sus campos, su estado y, si está firmada, su huella.</summary>
/// <remarks>Abrirla deja <c>record_viewed</c> en la auditoría (ADR 0040). Un borrador solo lo abre su autor; un firmado lo
/// abre cualquiera con permiso de lectura, y no se puede cambiar. <c>CanEdit</c> es lo que decide si la pantalla muestra
/// los campos para escribir o para leer.</remarks>
/// <param name="Id">El documento.</param>
/// <param name="Person">La persona a la que es.</param>
/// <param name="PersonName">Su nombre, para el encabezado.</param>
/// <param name="Status">Si es borrador o está firmado.</param>
/// <param name="AuthorName">El nombre de quien la escribió.</param>
/// <param name="CreatedAt">Cuándo se creó.</param>
/// <param name="SignedAt">Cuándo se firmó; <see langword="null"/> si es borrador.</param>
/// <param name="ContentHash">La huella de la firma; <see langword="null"/> si es borrador.</param>
/// <param name="Fields">Cada apartado, en el orden de la plantilla, con su texto.</param>
/// <param name="Missing">Los apartados obligatorios vacíos de un borrador: lo que la pantalla avisa antes de firmar. Vacío en lo firmado.</param>
/// <param name="CanEdit">Si quien mira es el autor de un borrador.</param>
public sealed record EncounterView(long Id, long Person, string PersonName, RecordStatus Status, string AuthorName,
    DateTimeOffset CreatedAt, DateTimeOffset? SignedAt, string? ContentHash,
    IReadOnlyList<KeyValuePair<EncounterSection, string>> Fields, IReadOnlyList<EncounterSection> Missing, bool CanEdit);
