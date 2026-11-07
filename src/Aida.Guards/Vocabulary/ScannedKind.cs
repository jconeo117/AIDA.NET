namespace Aida.Guards.Vocabulary;

/// <summary>Qué clase de texto encontró el escáner.</summary>
/// <remarks>El alcance del término decide en cuáles se busca. El orden de los miembros desempata en <see cref="SourceScanner.Scan"/>.</remarks>
public enum ScannedKind
{
    /// <summary>Un identificador: declaración o uso, <c>using</c> incluidos.</summary>
    Identifier,

    /// <summary>El valor de un literal de cadena: normal, verbatim, crudo, UTF-8 o el texto de una interpolada.</summary>
    StringLiteral,

    /// <summary>Un comentario <c>//</c>, <c>/* */</c> o de documentación XML.</summary>
    Comment,

    /// <summary>El valor de una concatenación de literales, ya unido.</summary>
    Concatenation,
}
