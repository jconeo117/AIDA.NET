using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;

namespace Aida.Arms.Agenda.Tests;

public sealed class TestDatabase : IDisposable
{
    public string DirectoryPath { get; }

    public string FilePath { get; }

    public PlatformDatabase Database { get; }

    public TestDatabase()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "aida-agenda-test-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(DirectoryPath);
        FilePath = Path.Combine(DirectoryPath, "aida.db");
        Database = new PlatformDatabase(FilePath);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
        catch
        {
            // Ignorar errores de limpieza de temporales
        }
    }
}
