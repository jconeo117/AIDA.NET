namespace Aida.Arms.Agenda.Storage;

/// <summary>Un tramo en el que no se agenda: una ausencia, unas vacaciones, un festivo.</summary>
/// <remarks>Es <c>Block</c> de la 0.1 (<c>store.py:59-67</c>). Sin recurso, el bloqueo es de todos, como el cierre del local.
/// El motivo va sellado en la base (ADR 0029), porque es texto libre y puede nombrar a un paciente; aquí ya está abierto.
/// Las dos reglas que lo usan viven aquí, en un solo sitio: la del hueco y la de la rejilla no pueden decir cosas
/// distintas (<c>tool.py:86-89</c>).</remarks>
/// <param name="Id">El identificador.</param>
/// <param name="Resource">El recurso, o vacío si es de todos.</param>
/// <param name="Start">Desde cuándo.</param>
/// <param name="End">Hasta cuándo; ese instante ya queda libre.</param>
/// <param name="Reason">El motivo, tal como se escribió y recortado; puede estar vacío.</param>
/// <param name="CreatedBy">Quién lo puso.</param>
public sealed record Block(long Id, string Resource, DateTimeOffset Start, DateTimeOffset End, string Reason, string CreatedBy)
{
    /// <summary>Si el bloqueo afecta a un recurso.</summary>
    /// <param name="resource">El recurso.</param>
    /// <returns><see langword="true"/> si es de todos o es de ese recurso.</returns>
    public bool AppliesTo(string resource) => Resource.Length == 0 || Resource == resource;

    /// <summary>Si el bloqueo se cruza con un intervalo.</summary>
    /// <param name="start">El inicio del intervalo.</param>
    /// <param name="end">El fin del intervalo.</param>
    /// <returns><see langword="true"/> si cada uno empieza antes de que acabe el otro: tocarse en el borde no es cruzarse.</returns>
    public bool Crosses(DateTimeOffset start, DateTimeOffset end) => Start < end && start < End;
}
