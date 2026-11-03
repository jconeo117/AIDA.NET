namespace Aida.Guards;

/// <summary>Un incumplimiento de una regla del repositorio.</summary>
/// <param name="Rule">El id de la regla en <c>00-task.md</c> (por ejemplo <c>C1</c> o <c>A2</c>).</param>
/// <param name="Location">Ruta relativa con <c>/</c>, nombre de proyecto o nombre completo de tipo.</param>
/// <param name="Line">Línea, empezando en 1; 0 si la regla no es de un punto de un archivo.</param>
/// <param name="Message">Qué está mal y cómo se arregla, en español.</param>
/// <remarks>
/// Un solo tipo para todos los guardianes: el test solo necesita imprimir la lista. El mensaje dice
/// cómo se arregla porque quien lo lee es quien acaba de romper la regla, no quien la escribió.
/// </remarks>
public sealed record Violation(string Rule, string Location, int Line, string Message)
{
    /// <summary>Escribe el incumplimiento en una línea: <c>[C1] src/X.cs:12 — mensaje</c>.</summary>
    /// <returns>El texto para el informe del test.</returns>
    /// <remarks>Formato fijo, para poder buscarlo con <c>grep</c> en la salida del CI. Sin línea (0), se omite <c>:0</c> para no fingir un punto del archivo.</remarks>
    public override string ToString() =>
        Line > 0
            ? $"[{Rule}] {Location}:{Line} — {Message}"
            : $"[{Rule}] {Location} — {Message}";
}
