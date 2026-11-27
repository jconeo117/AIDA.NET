namespace Aida.Platform.Records;

/// <summary>Lo que se ve de un documento sin abrir su contenido.</summary>
/// <remarks>Lo que va en claro es lo que una lista necesita para que alguien elija un documento: su clase, de quién es, quién
/// lo escribió y cuándo. El contenido no sale aquí, así que una lista no abre texto sellado ni deja rastro del contenido.</remarks>
/// <param name="Id">El identificador del documento.</param>
/// <param name="Kind">La clase de documento, que declara el brazo.</param>
/// <param name="Person">La persona a la que es el documento.</param>
/// <param name="Status">Si es borrador o está firmado.</param>
/// <param name="Author">Quién lo escribió.</param>
/// <param name="Reference">Un texto opaco que puso el brazo para enlazarlo con algo de fuera; o <see langword="null"/>.</param>
/// <param name="CreatedAt">Cuándo se creó.</param>
/// <param name="UpdatedAt">Cuándo se guardó por última vez.</param>
/// <param name="SignedAt">Cuándo se firmó; <see langword="null"/> si es borrador.</param>
public sealed record RecordSummary(long Id, string Kind, long Person, RecordStatus Status, string Author, string? Reference,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? SignedAt);
