namespace Aida.Platform.Records;

/// <summary>Un documento abierto: su resumen, sus campos y, si está firmado, su huella.</summary>
/// <remarks>Abrirlo descifra el contenido, y por eso la lectura pasa antes por la auditoría (<c>record_viewed</c>). Los campos
/// son los del borrador o los firmados; la huella solo existe en lo firmado, y es la que se vuelve a calcular para verificar.</remarks>
/// <param name="Summary">El resumen del documento.</param>
/// <param name="Fields">Los campos con su texto, por su nombre.</param>
/// <param name="ContentHash">La huella de la firma, o <see langword="null"/> si es borrador.</param>
public sealed record RecordContent(RecordSummary Summary, IReadOnlyDictionary<string, string> Fields, string? ContentHash);
