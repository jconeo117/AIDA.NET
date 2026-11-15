using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Platform.Tests.Storage;

public sealed class PlatformDatabaseTests
{
    [Fact]
    public void Crea_el_esquema_en_la_version_1()
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
                Assert.Equal(1, ver);

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
            // Creamos un archivo SQLite con versión 2
            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA user_version = 2;";
                cmd.ExecuteNonQuery();
            }

            var ex = Assert.Throws<InvalidOperationException>(() => new PlatformDatabase(dbPath));
            Assert.Contains("la base de datos es de la versión 2 y este AIDA solo conoce hasta la 1", ex.Message);
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
