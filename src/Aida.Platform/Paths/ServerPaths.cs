namespace Aida.Platform.Paths;

/// <summary>Dónde lee y dónde escribe el servidor.</summary>
/// <remarks>
/// Una sola puerta para las dos preguntas, como <c>resources.py:16-62</c> de la 0.1, que nació porque doce módulos
/// buscaban el pack subiendo directorios. Aquí no hay carpeta del usuario: escribe el servicio del servidor (ADR 0020).
/// Las dos rutas se inyectan, para que los tests no toquen nunca los datos reales (lección 11).
/// </remarks>
public sealed class ServerPaths
{
    /// <summary>El nombre del archivo de la base de datos dentro de <see cref="DataDirectory"/>.</summary>
    public const string DatabaseFileName = "aida.db";

    /// <summary>Crea las rutas.</summary>
    /// <param name="packsDirectory">La carpeta que contiene <c>platform/</c> y los packs de los brazos.</param>
    /// <param name="dataDirectory">La carpeta donde el servidor escribe. Se crea si no existe.</param>
    /// <exception cref="ArgumentException">Si alguna ruta está vacía o no es absoluta.</exception>
    public ServerPaths(string packsDirectory, string dataDirectory)
    {
        PacksDirectory = Absolute(packsDirectory, nameof(packsDirectory));
        DataDirectory = Absolute(dataDirectory, nameof(dataDirectory));
    }

    /// <summary>La carpeta de los packs, de solo lectura.</summary>
    public string PacksDirectory { get; }

    /// <summary>La carpeta donde escribe el servidor.</summary>
    public string DataDirectory { get; }

    /// <summary>La ruta del archivo de la base de datos.</summary>
    public string DatabaseFile => Path.Combine(DataDirectory, DatabaseFileName);

    /// <summary>La ruta de un archivo de <c>packs/platform/</c>.</summary>
    /// <param name="fileName">El nombre del archivo, por ejemplo <c>ui.yaml</c>.</param>
    /// <returns>La ruta completa.</returns>
    public string PlatformPackFile(string fileName) => Path.Combine(PacksDirectory, "platform", fileName);

    /// <summary>La carpeta del pack de un brazo, de solo lectura.</summary>
    /// <param name="armId">El identificador del brazo.</param>
    /// <returns>La ruta completa.</returns>
    public string ArmPackDirectory(string armId) => Path.Combine(PacksDirectory, armId);

    /// <summary>La ruta de un archivo de configuración de esta instalación, que el servidor copia de una plantilla y se edita a mano.</summary>
    /// <param name="fileName">El nombre del archivo.</param>
    /// <returns>La ruta completa, dentro de <c>config/</c> de la carpeta de datos.</returns>
    public string ConfigFile(string fileName) => Path.Combine(DataDirectory, "config", fileName);

    /// <summary>La ruta del archivo de la clave del equipo.</summary>
    public string MachineKeyFile => Path.Combine(DataDirectory, "keys", "machine.key");

    private static string Absolute(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"la ruta tiene que ser absoluta: «{path}»", name);
        }

        return Path.GetFullPath(path);
    }
}
