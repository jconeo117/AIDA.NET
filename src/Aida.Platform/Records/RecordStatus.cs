namespace Aida.Platform.Records;

/// <summary>En qué está un documento.</summary>
/// <remarks>Un documento firmado no vuelve a borrador: es un estado final. Por eso lo firmado lo impiden los triggers de la base,
/// y no solo este código.</remarks>
public enum RecordStatus
{
    /// <summary>Lo escribe su autor y solo él lo ve.</summary>
    Draft = 0,

    /// <summary>Firmado con huella: inalterable en la base.</summary>
    Signed = 1,
}
