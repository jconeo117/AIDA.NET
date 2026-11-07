using System.Xml.Linq;

namespace Aida.Guards.Dependencies;

/// <summary>Lee las referencias a proyectos de un <c>.csproj</c>.</summary>
/// <remarks>
/// Lee el XML y no el ensamblado: el compilador quita del <c>.dll</c> las referencias que no se usan, así
/// que una dependencia indebida no se vería hasta que alguien usara un tipo de ella.
/// </remarks>
public static class ProjectReferenceReader
{
    /// <summary>Devuelve los nombres de los proyectos referenciados.</summary>
    /// <param name="csprojXml">El contenido del <c>.csproj</c>.</param>
    /// <returns>El nombre de archivo sin extensión de cada <c>ProjectReference Include</c> (admite <c>\</c> y <c>/</c>), sin repetidos y ordenado por ordinal.</returns>
    /// <exception cref="System.Xml.XmlException">Si el XML no es válido.</exception>
    /// <remarks>Un csproj roto es un fallo y no una lista vacía, que pasaría siempre.</remarks>
    public static IReadOnlyList<string> Read(string csprojXml)
    {
        return XDocument.Parse(csprojXml).Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
