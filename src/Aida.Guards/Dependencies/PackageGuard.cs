namespace Aida.Guards.Dependencies;

/// <summary>Comprueba los paquetes de un proyecto contra las familias restringidas.</summary>
/// <remarks>Las reglas se inyectan para probar con una de mentira.</remarks>
public sealed class PackageGuard
{
    private readonly IReadOnlyList<PackageRule> _rules;

    /// <summary>Crea el guardián.</summary>
    /// <param name="rules">Las familias de paquetes.</param>
    /// <remarks>En el repositorio se usa <see cref="PackagePolicy.Rules"/>.</remarks>
    public PackageGuard(IReadOnlyList<PackageRule> rules)
    {
        _rules = rules;
    }

    /// <summary>Comprueba un proyecto.</summary>
    /// <param name="project">Nombre del proyecto.</param>
    /// <param name="packages">Sus paquetes, de <see cref="LockFileReader.ReadPackages"/>.</param>
    /// <returns>Un incumplimiento, con el <c>Rule</c> de la familia, por cada paquete que el proyecto no puede tener; en orden de paquete y luego de regla.</returns>
    /// <remarks>El mensaje dice en qué proyectos puede vivir ese paquete.</remarks>
    public IReadOnlyList<Violation> Check(string project, IReadOnlyList<string> packages)
    {
        var violations = new List<Violation>();
        foreach (var package in packages)
        {
            foreach (var rule in _rules)
            {
                if (rule.Matches(package) && !rule.AllowedProjects.Contains(project))
                {
                    var allowed = string.Join(", ", rule.AllowedProjects.Order(StringComparer.Ordinal));
                    violations.Add(new Violation(
                        rule.Rule,
                        project,
                        0,
                        $"El paquete {package} solo puede estar en {allowed}; {project} no puede tenerlo. Quítalo de {project} o muévelo al proyecto que lo usa (véase la regla {rule.Rule} en 00-task.md)."));
                }
            }
        }

        return violations;
    }
}
