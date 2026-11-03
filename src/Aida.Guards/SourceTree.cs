using System.Text;

namespace Aida.Guards;

/// <summary>Los archivos del repositorio que miran los guardianes.</summary>
/// <remarks>
/// Excluye toda ruta con un segmento <c>bin</c> u <c>obj</c>: ahí hay código generado que nadie escribe
/// a mano. Las rutas se devuelven relativas y con <c>/</c>, para que el informe sea igual en Windows y
/// en Linux.
/// </remarks>
public sealed class SourceTree
{
    /// <summary>Crea el árbol sobre una raíz.</summary>
    /// <param name="root">Ruta absoluta de la raíz del repositorio.</param>
    /// <exception cref="DirectoryNotFoundException">Si <paramref name="root"/> no existe.</exception>
    /// <remarks>La raíz se inyecta para poder probar con una carpeta temporal.</remarks>
    public SourceTree(string root)
    {
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"La raíz «{root}» no existe.");
        }

        Root = Path.GetFullPath(root);
    }

    /// <summary>La raíz absoluta.</summary>
    /// <remarks>Se expone para que el test componga mensajes y rutas sin guardar la raíz por su cuenta.</remarks>
    public string Root { get; }

    /// <summary>Lista los archivos bajo una carpeta con alguna de las extensiones dadas.</summary>
    /// <param name="relativeDirectory">Carpeta relativa a la raíz, con <c>/</c> (por ejemplo <c>src/Aida.Engine</c>).</param>
    /// <param name="extensions">Extensiones con punto (<c>.cs</c>), comparadas sin distinguir mayúsculas.</param>
    /// <returns>Rutas relativas a la raíz, con <c>/</c>, ordenadas por ordinal. Vacía si la carpeta no existe.</returns>
    /// <remarks>Una carpeta que no existe da una lista vacía porque un proyecto puede no tener aún archivos; el falso verde lo cierra N003.</remarks>
    public IReadOnlyList<string> ListFiles(string relativeDirectory, IReadOnlyCollection<string> extensions)
    {
        var directory = Path.Combine(Root, relativeDirectory);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(Root, file).Replace('\\', '/'))
            .Where(relative => extensions.Contains(Path.GetExtension(relative), StringComparer.OrdinalIgnoreCase))
            .Where(relative => !relative.Split('/').Any(IsBuildOutput))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Lee un archivo como UTF-8.</summary>
    /// <param name="relativePath">Ruta relativa a la raíz, con <c>/</c>.</param>
    /// <returns>El contenido.</returns>
    /// <exception cref="FileNotFoundException">Si no existe.</exception>
    /// <remarks>Un solo punto de lectura, para que todos los guardianes lean igual.</remarks>
    public string ReadAllText(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath), Encoding.UTF8);

    private static bool IsBuildOutput(string segment) =>
        segment.Equals("bin", StringComparison.OrdinalIgnoreCase) || segment.Equals("obj", StringComparison.OrdinalIgnoreCase);
}
