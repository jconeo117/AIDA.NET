using Microsoft.Data.Sqlite;

namespace Aida.Platform.Storage;

/// <summary>La base de datos del servidor: un archivo SQLite con su esquema.</summary>
/// <remarks>
/// Un solo archivo en el servidor, con transacciones (ADR 0028). La plataforma pone el archivo, el modo WAL, las claves
/// foráneas y la espera; cada brazo escribe el SQL de sus propias tablas con la conexión que abre aquí, y versiona su
/// esquema con <see cref="ApplyArmSchema"/> (ADR 0032). Cada operación abre su conexión y la cierra; SQLite las reutiliza
/// de su grupo, y así no hay una conexión compartida entre sesiones.
/// </remarks>
public sealed class PlatformDatabase
{
    private readonly string _connectionString;

    /// <summary>Abre o crea la base de datos y deja su esquema al día.</summary>
    /// <param name="filePath">La ruta del archivo. Su carpeta se crea si no existe.</param>
    /// <exception cref="InvalidOperationException">Si el archivo es de una versión de esquema más nueva.</exception>
    public PlatformDatabase(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = filePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            ForeignKeys = true,
            DefaultTimeout = 5,
        }.ToString();
        using var connection = Open();
        Schema.Apply(connection);
    }

    /// <summary>Abre una conexión nueva. Quien la pide la cierra.</summary>
    /// <returns>La conexión abierta, con las claves foráneas activas y la espera de 5 s.</returns>
    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>Deja al día el esquema de un brazo.</summary>
    /// <param name="armId">El identificador del brazo (<c>^[a-z][a-z0-9_]*$</c>).</param>
    /// <param name="steps">El SQL de cada versión, en orden: el primero lleva a la versión 1.</param>
    /// <exception cref="ArgumentException">Si el identificador no es válido o no hay pasos.</exception>
    /// <exception cref="InvalidOperationException">Si el archivo tiene una versión del brazo más nueva que sus pasos.</exception>
    /// <remarks>Los pasos que faltan y la versión nueva van en una sola transacción: un esquema a medias no existe.</remarks>
    public void ApplyArmSchema(string armId, IReadOnlyList<string> steps)
    {
        using var connection = Open();
        ArmSchema.Apply(connection, armId, steps);
    }
}
