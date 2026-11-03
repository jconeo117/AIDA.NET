namespace Aida.Guards.Dependencies;

/// <summary>Una familia de paquetes y los únicos proyectos que pueden tenerla.</summary>
/// <param name="Rule">El id de la regla (<c>A1</c> o <c>D4</c>).</param>
/// <param name="Prefixes">Ids o prefijos de id de paquete.</param>
/// <param name="AllowedProjects">Proyectos que pueden tener estos paquetes.</param>
/// <remarks>Por prefijo, porque las familias se publican en varios paquetes (<c>Microsoft.Extensions.AI.*</c>).</remarks>
public sealed record PackageRule(string Rule, IReadOnlyList<string> Prefixes, IReadOnlySet<string> AllowedProjects)
{
    /// <summary>Indica si un paquete es de esta familia.</summary>
    /// <param name="packageId">Id del paquete.</param>
    /// <returns><c>true</c> si es igual a un prefijo o empieza por el prefijo más un punto, sin distinguir mayúsculas (ordinal).</returns>
    /// <remarks>Con el punto, para que <c>Mistral</c> no case con un paquete ajeno llamado <c>MistralFoo</c>.</remarks>
    public bool Matches(string packageId) =>
        Prefixes.Any(prefix =>
            packageId.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || packageId.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase));
}
