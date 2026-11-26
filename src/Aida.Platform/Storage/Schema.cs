using System.Collections.Immutable;
using Microsoft.Data.Sqlite;

namespace Aida.Platform.Storage;

/// <summary>El esquema de la base de datos y su versión.</summary>
/// <remarks>La versión va en <c>PRAGMA user_version</c>. Un archivo más nuevo que este código no se abre: un dato
/// mal leído es peor que no leerlo (<c>persistence.py:33-35</c> de la 0.1 lo dice del guardado).</remarks>
internal static class Schema
{
    internal const int Version = 3;

    private const string _version1 = """
        CREATE TABLE users (
            id INTEGER PRIMARY KEY,
            user_name TEXT NOT NULL UNIQUE,
            display_name TEXT NOT NULL,
            role TEXT NOT NULL,
            password TEXT NOT NULL,
            active INTEGER NOT NULL CHECK (active IN (0, 1))
        ) STRICT;
        CREATE TABLE ledger (
            seq INTEGER PRIMARY KEY,
            digest TEXT NOT NULL UNIQUE,
            line TEXT NOT NULL
        ) STRICT;
        """;

    private const string _version2 = """
        CREATE TABLE arm_schema (
            arm TEXT PRIMARY KEY,
            version INTEGER NOT NULL
        ) STRICT;
        """;

    private const string _version3 = """
        CREATE TABLE people (
            id INTEGER PRIMARY KEY,
            name BLOB NOT NULL,
            phone BLOB NOT NULL,
            phone_index TEXT NOT NULL,
            document_type TEXT,
            document_number BLOB,
            document_index TEXT UNIQUE,
            birth_date BLOB,
            sex BLOB,
            CHECK ((document_type IS NULL) = (document_number IS NULL) AND (document_number IS NULL) = (document_index IS NULL))
        ) STRICT;
        CREATE INDEX people_phone ON people(phone_index);
        """;

    private static readonly ImmutableArray<string> _steps = [_version1, _version2, _version3];

    internal static void Apply(SqliteConnection connection)
    {
        using (var wal = connection.CreateCommand())
        {
            wal.CommandText = "PRAGMA journal_mode = WAL;";
            wal.ExecuteNonQuery();
        }

        using var transaction = connection.BeginTransaction();
        var current = ReadVersion(connection, transaction);
        if (current > Version)
        {
            throw new InvalidOperationException($"la base de datos es de la versión {current} y este AIDA solo conoce hasta la {Version}");
        }

        if (current < Version)
        {
            using var create = connection.CreateCommand();
            create.Transaction = transaction;
            create.CommandText = string.Concat(_steps.Skip((int)current)) + $"PRAGMA user_version = {Version};";
            create.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static long ReadVersion(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version;";
        return (long)command.ExecuteScalar()!;
    }
}
