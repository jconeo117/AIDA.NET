using Microsoft.Data.Sqlite;

namespace Aida.Platform.Storage;

/// <summary>La base de datos del servidor: un archivo SQLite con su esquema.</summary>
/// <remarks>
/// Un solo archivo en el servidor, con transacciones (ADR 0028). La conexión no sale de <c>Aida.Platform</c>: los
/// demás proyectos usan los almacenes (<c>UserStore</c>, <c>AuditLog</c>), nunca SQL. Cada operación abre su
/// conexión y la cierra; SQLite las reutiliza de su grupo, y así no hay una conexión compartida entre sesiones.
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
    /// <returns>La conexión abierta.</returns>
    internal SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
