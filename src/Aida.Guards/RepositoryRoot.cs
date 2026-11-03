namespace Aida.Guards;

/// <summary>Encuentra la raíz del repositorio.</summary>
/// <remarks>
/// Los tests corren desde <c>bin/</c>, y las rutas relativas escritas a mano se rompen al mover un
/// proyecto. La raíz es la carpeta que contiene <c>Aida.slnx</c>.
/// </remarks>
public static class RepositoryRoot
{
    /// <summary>Sube desde una carpeta hasta la que contiene <c>Aida.slnx</c>.</summary>
    /// <param name="startDirectory">Carpeta de partida; normalmente <c>AppContext.BaseDirectory</c>.</param>
    /// <returns>La ruta absoluta de la raíz.</returns>
    /// <exception cref="DirectoryNotFoundException">Si ninguna carpeta superior contiene <c>Aida.slnx</c>.</exception>
    /// <remarks>Falla en lugar de devolver una ruta por defecto: un guardián que mira la carpeta equivocada siempre pasa.</remarks>
    public static string Find(string startDirectory)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(startDirectory)); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "Aida.slnx")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException($"Ninguna carpeta por encima de «{startDirectory}» contiene Aida.slnx.");
    }
}
