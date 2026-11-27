using Aida.Platform.Records;

namespace Aida.Arms.Health.Desk;

/// <summary>Una línea de la lista de atenciones de una persona: lo que se ve sin abrir el documento.</summary>
/// <remarks>Un borrador de otro profesional aparece, con su autor y su fecha, para que se sepa que existe. Su campo
/// <c>Mine</c> es lo que decide si la pantalla lo enlaza: no se abre lo que no es de quien lo mira.</remarks>
/// <param name="Id">El documento.</param>
/// <param name="Status">Si es borrador o está firmado.</param>
/// <param name="AuthorName">El nombre de quien la escribió.</param>
/// <param name="CreatedAt">Cuándo se creó.</param>
/// <param name="SignedAt">Cuándo se firmó; <see langword="null"/> si es borrador.</param>
/// <param name="Mine">Si la escribió quien mira la lista.</param>
public sealed record EncounterLine(long Id, RecordStatus Status, string AuthorName, DateTimeOffset CreatedAt, DateTimeOffset? SignedAt, bool Mine);
