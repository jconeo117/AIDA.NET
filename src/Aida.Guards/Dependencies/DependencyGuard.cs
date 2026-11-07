namespace Aida.Guards.Dependencies;

/// <summary>Comprueba que las referencias de un proyecto respetan una política.</summary>
/// <remarks>La política se inyecta para probar con una de mentira.</remarks>
public sealed class DependencyGuard
{
    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _allowed;

    /// <summary>Crea el guardián.</summary>
    /// <param name="allowed">Proyecto y proyectos que puede referenciar.</param>
    /// <remarks>En el repositorio se usa <see cref="DependencyPolicy.Allowed"/>.</remarks>
    public DependencyGuard(IReadOnlyDictionary<string, IReadOnlySet<string>> allowed)
    {
        _allowed = allowed;
    }

    /// <summary>Comprueba un proyecto.</summary>
    /// <param name="project">Nombre del proyecto (<c>Aida.Engine</c>).</param>
    /// <param name="references">Sus referencias, de <see cref="ProjectReferenceReader.Read"/>.</param>
    /// <returns>Un <c>C3</c> si el proyecto no tiene política; si la tiene, un <c>C3</c> por cada referencia no permitida.</returns>
    /// <remarks>El mensaje nombra la referencia y remite a <c>docs/architecture/README.md</c>.</remarks>
    public IReadOnlyList<Violation> Check(string project, IReadOnlyList<string> references)
    {
        if (!_allowed.TryGetValue(project, out var permitted))
        {
            return [new Violation("C3", project, 0, $"{project} no tiene política de dependencias. Añádelo a DependencyPolicy.Allowed y a docs/architecture/README.md antes de crear referencias (ADR 0011).")];
        }

        return references
            .Where(reference => !permitted.Contains(reference))
            .Order(StringComparer.Ordinal)
            .Select(reference => new Violation("C3", project, 0, $"{project} referencia a {reference}, y esa dependencia no está permitida. Quita la referencia o cambia la capa; el diagrama está en docs/architecture/README.md."))
            .ToList();
    }
}
