using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;

namespace Aida.Platform.Snapshots;

/// <summary>Comprueba que la base de una copia se puede abrir y no es de un AIDA más nuevo.</summary>
/// <remarks>
/// La base se abre solo de lectura y sin agrupar conexiones: así se cierra del todo antes de que la carpeta se mueva, y
/// en Windows no queda un archivo abierto. Una base de un esquema más nuevo no se abre: sus datos no se entienden
/// (ADR 0036, y <see cref="Schema"/>).
/// </remarks>
internal static class StagedDatabase
{
    /// <summary>Comprueba la base preparada.</summary>
    /// <param name="path">La ruta de <c>aida.db</c> en la carpeta de paso.</param>
    /// <param name="armSchemaVersions">La versión de esquema que conoce este AIDA de cada brazo.</param>
    /// <exception cref="SnapshotException"><see cref="SnapshotError.TooNew"/> si el esquema es más nuevo;
    /// <see cref="SnapshotError.BadFile"/> si no se abre o no pasa <c>quick_check</c>.</exception>
    /// <remarks>Un brazo que este AIDA no conoce se ignora: su esquema no lo entiende nadie aquí, y no hay nada que comparar.</remarks>
    internal static void Check(string path, IReadOnlyDictionary<string, int> armSchemaVersions)
    {
        var text = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();
        try
        {
            using var connection = new SqliteConnection(text);
            connection.Open();
            if ((long)Read(connection, "PRAGMA user_version;")! > Schema.Version)
            {
                throw new SnapshotException(SnapshotError.TooNew);
            }

            if ((long)Read(connection, "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'arm_schema');")! == 1)
            {
                CheckArms(connection, armSchemaVersions);
            }

            if (Read(connection, "PRAGMA quick_check;") as string != "ok")
            {
                throw new SnapshotException(SnapshotError.BadFile);
            }
        }
        catch (SqliteException exception)
        {
            throw new SnapshotException(SnapshotError.BadFile, exception);
        }
    }

    private static void CheckArms(SqliteConnection connection, IReadOnlyDictionary<string, int> armSchemaVersions)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT arm, version FROM arm_schema;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var arm = reader.GetString(0);
            var version = reader.GetInt64(1);
            if (armSchemaVersions.TryGetValue(arm, out var known) && version > known)
            {
                throw new SnapshotException(SnapshotError.TooNew);
            }
        }
    }

    private static object? Read(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
