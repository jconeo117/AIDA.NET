using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Platform.Tests.Storage;

public sealed class PlatformDatabaseTests
{
    [Fact]
    public void Crea_el_esquema_en_la_version_2()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida-schema-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "aida.db");
        try
        {
            var db = new PlatformDatabase(dbPath);
            using (var conn = db.Open())
            {
                using var cmdVer = conn.CreateCommand();
                cmdVer.CommandText = "PRAGMA user_version;";
                var ver = (long)cmdVer.ExecuteScalar()!;
                Assert.Equal(3, ver);

                using var cmdTables = conn.CreateCommand();
                cmdTables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
                using var reader = cmdTables.ExecuteReader();
                var tables = new List<string>();
                while (reader.Read())
                {
                    tables.Add(reader.GetString(0));
                }

                Assert.Contains("users", tables);
                Assert.Contains("ledger", tables);
                Assert.Contains("arm_schema", tables);
                Assert.Contains("people", tables);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Abrir_dos_veces_no_lo_rehace()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida-schema-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "aida.db");
        try
        {
            var db1 = new PlatformDatabase(dbPath);
            using (var conn1 = db1.Open())
            {
                using var insert = conn1.CreateCommand();
                insert.CommandText = "INSERT INTO users (user_name, display_name, role, password, active) VALUES ('test', 'Test', 'admin', 'hash', 1);";
                insert.ExecuteNonQuery();
            }

            // Abrir por segunda vez
            var db2 = new PlatformDatabase(dbPath);
            using (var conn2 = db2.Open())
            {
                using var select = conn2.CreateCommand();
                select.CommandText = "SELECT count(*) FROM users;";
                var count = (long)select.ExecuteScalar()!;
                Assert.Equal(1, count);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Un_archivo_de_una_version_mas_nueva_no_se_abre()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida-schema-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "aida.db");
        try
        {
            // Creamos un archivo SQLite con versión 4
            using (var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA user_version = 4;";
                cmd.ExecuteNonQuery();
            }

            var ex = Assert.Throws<InvalidOperationException>(() => new PlatformDatabase(dbPath));
            Assert.Contains("la base de datos es de la versión 4 y este AIDA solo conoce hasta la 3", ex.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Un_archivo_de_la_version_1_sube_a_la_3()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida-migrate-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "aida.db");
        try
        {
            using (var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
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
                    PRAGMA user_version = 1;
                    """;
                cmd.ExecuteNonQuery();

                using var insert = conn.CreateCommand();
                insert.CommandText = "INSERT INTO users (user_name, display_name, role, password, active) VALUES ('u1', 'Ana', 'admin', 'pass', 1);";
                insert.ExecuteNonQuery();
            }

            var db = new PlatformDatabase(dbPath);
            using (var conn = db.Open())
            {
                using var cmdVer = conn.CreateCommand();
                cmdVer.CommandText = "PRAGMA user_version;";
                Assert.Equal(3L, (long)cmdVer.ExecuteScalar()!);

                using var cmdTables = conn.CreateCommand();
                cmdTables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
                using var reader = cmdTables.ExecuteReader();
                var tables = new List<string>();
                while (reader.Read())
                {
                    tables.Add(reader.GetString(0));
                }

                Assert.Contains("arm_schema", tables);
                Assert.Contains("users", tables);
                Assert.Contains("ledger", tables);
                Assert.Contains("people", tables);

                using var cmdUser = conn.CreateCommand();
                cmdUser.CommandText = "SELECT count(*) FROM users WHERE user_name = 'u1';";
                Assert.Equal(1L, (long)cmdUser.ExecuteScalar()!);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
