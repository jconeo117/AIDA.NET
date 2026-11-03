namespace Aida.Guards.Vocabulary;

/// <summary>Regla A2: el núcleo no nombra un dominio.</summary>
/// <remarks>
/// Un término casa si sus palabras aparecen seguidas, y enteras, entre las palabras del texto
/// (<see cref="WordSplitter.Split"/>). Así casa <c>PatientRecord</c> y <c>Param</c> no casa con <c>ram</c>. Los
/// términos <see cref="TermScope.Code"/> no se buscan en los <see cref="ScannedKind.Comment"/>.
/// </remarks>
public sealed class VocabularyGuard
{
    private readonly IReadOnlyList<ForbiddenTerm> _terms;

    /// <summary>Crea el guardián.</summary>
    /// <param name="terms">Los términos prohibidos.</param>
    /// <remarks>En el repositorio se usa <see cref="ForbiddenTermCatalog.LoadDefault"/>; se inyectan para probar con una lista pequeña.</remarks>
    public VocabularyGuard(IReadOnlyList<ForbiddenTerm> terms)
    {
        _terms = terms;
    }

    /// <summary>Comprueba un archivo.</summary>
    /// <param name="path">Ruta relativa, para el informe.</param>
    /// <param name="sourceText">El código.</param>
    /// <returns>Un <c>A2</c> por cada pareja de texto escaneado y término que casa, en orden de línea y luego de término.</returns>
    /// <remarks>El mensaje cita el término y el texto, y dice que ese vocabulario va en un pack (ADR 0002).</remarks>
    public IReadOnlyList<Violation> Check(string path, string sourceText)
    {
        var hits = new List<(int Line, int TermIndex, Violation Violation)>();
        foreach (var scanned in SourceScanner.Scan(sourceText))
        {
            var words = WordSplitter.Split(scanned.Text);
            for (var index = 0; index < _terms.Count; index++)
            {
                var term = _terms[index];
                if (term.Scope == TermScope.Code && scanned.Kind == ScannedKind.Comment)
                {
                    continue;
                }

                if (ContainsSequence(words, term.Words))
                {
                    hits.Add((scanned.Line, index, new Violation("A2", path, scanned.Line, Message(term, scanned.Text))));
                }
            }
        }

        return hits
            .OrderBy(hit => hit.Line)
            .ThenBy(hit => hit.TermIndex)
            .Select(hit => hit.Violation)
            .ToList();
    }

    private static bool ContainsSequence(IReadOnlyList<string> words, IReadOnlyList<string> term)
    {
        if (term.Count == 0)
        {
            return false;
        }

        for (var start = 0; start + term.Count <= words.Count; start++)
        {
            if (words.Skip(start).Take(term.Count).SequenceEqual(term, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Message(ForbiddenTerm term, string text) =>
        $"Vocabulario de un brazo, «{string.Join(" ", term.Words)}», en «{Abbreviate(text)}». Ese término va en un pack (ADR 0002), no en el núcleo: usa un nombre genérico o muévelo a un pack.";

    private static string Abbreviate(string text)
    {
        var flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length <= 80 ? flat : flat[..80] + "…";
    }
}
