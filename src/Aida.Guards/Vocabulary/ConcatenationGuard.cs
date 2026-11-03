namespace Aida.Guards.Vocabulary;

/// <summary>Prohíbe concatenar literales de cadena en el núcleo.</summary>
/// <remarks>
/// Partir una palabra (<c>"c" + "pu"</c>) solo sirve para esconderla del guardián de vocabulario. En Python
/// se prohibió por eso (I-092). Un texto largo cabe en un literal crudo.
/// </remarks>
public sealed class ConcatenationGuard
{
    /// <summary>Comprueba un archivo.</summary>
    /// <param name="path">Ruta relativa, para el informe.</param>
    /// <param name="sourceText">El código.</param>
    /// <returns>Un <c>A2</c> por cada <see cref="ScannedKind.Concatenation"/>, con el valor unido en el mensaje.</returns>
    /// <remarks>Es un guardián aparte porque falla por la forma, sea cual sea la palabra que resulte.</remarks>
    public IReadOnlyList<Violation> Check(string path, string sourceText) =>
        SourceScanner.Scan(sourceText)
            .Where(scanned => scanned.Kind == ScannedKind.Concatenation)
            .Select(scanned => new Violation(
                "A2",
                path,
                scanned.Line,
                $"Concatenación de literales «{scanned.Text}» en el núcleo: se prohíbe partir un texto para esconderlo del guardián de vocabulario. Un texto largo va en un literal crudo (\"\"\"…\"\"\"); una palabra prohibida, en un pack (ADR 0002)."))
            .ToList();
}
