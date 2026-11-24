using System.Globalization;
using Aida.Platform.Paths;
using Microsoft.Data.Sqlite;

namespace Aida.Platform.Snapshots;

/// <summary>Aplica al arrancar la copia que dejó preparada <see cref="SnapshotStager"/>.</summary>
/// <remarks>
/// <para>Corre antes de que nadie abra la base ni la clave: solo mueve archivos. La 0.1 restauraba en su sitio y se
/// cerraba (<c>shell.py:655-665</c>); aquí la base está abierta mientras AIDA corre (ADR 0036).</para>
/// <para>Nunca pisa un equipo con usuarios, y nunca borra: lo que había va a <c>antes-&lt;fecha&gt;</c>, como
/// <c>config.antes-&lt;fecha&gt;</c> de la 0.1 (<c>snapshot.py:131</c>).</para>
/// </remarks>
public static class SnapshotInstaller
{
    /// <summary>Aplica lo preparado, si lo hay.</summary>
    /// <param name="paths">Las rutas.</param>
    /// <param name="time">El reloj, para el nombre de lo apartado y la fecha del aviso.</param>
    /// <returns>Qué pasó.</returns>
    /// <remarks>Si se corta a la mitad, el siguiente arranque vuelve a empezar: lo ya movido está en <c>antes-</c> o en su sitio.</remarks>
    public static PendingRestore ApplyPending(ServerPaths paths, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(time);
        var staging = paths.RestoreStagingDirectory;
        if (!Directory.Exists(staging))
        {
            return PendingRestore.None;
        }

        var stamp = time.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        if (File.Exists(paths.DatabaseFile) && HasUsers(paths.DatabaseFile))
        {
            Directory.Move(staging, Path.Combine(paths.DataDirectory, $"restaurar.descartada-{stamp}"));
            return PendingRestore.Discarded;
        }

        var notice = SnapshotManifest.Parse(File.ReadAllBytes(Path.Combine(staging, "notice.json")));
        var previous = MoveAside(paths, stamp);
        Install(paths, staging);
        var pending = new Dictionary<string, string>(notice, StringComparer.Ordinal)
        {
            ["previous"] = previous,
            ["restored_at"] = SnapshotManifest.Timestamp(time.GetLocalNow()),
        };
        File.WriteAllBytes(paths.RestorePendingFile, SnapshotManifest.Serialize(pending));
        Directory.Delete(staging, recursive: true);
        return PendingRestore.Installed;
    }

    private static bool HasUsers(string databaseFile)
    {
        var text = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFile,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();
        try
        {
            using var connection = new SqliteConnection(text);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM users);";
            return (long)command.ExecuteScalar()! == 1;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private static string MoveAside(ServerPaths paths, string stamp)
    {
        var keysDirectory = Path.GetDirectoryName(paths.MachineKeyFile)!;
        var current = new[] { paths.DatabaseFile, $"{paths.DatabaseFile}-wal", $"{paths.DatabaseFile}-shm", paths.ConfigDirectory, keysDirectory }
            .Where(item => File.Exists(item) || Directory.Exists(item))
            .ToList();
        if (current.Count == 0)
        {
            return string.Empty;
        }

        var folder = Path.Combine(paths.DataDirectory, $"antes-{stamp}");
        Directory.CreateDirectory(folder);
        foreach (var item in current)
        {
            Move(item, Path.Combine(folder, Path.GetFileName(item)));
        }

        return Path.GetFileName(folder);
    }

    private static void Install(ServerPaths paths, string staging)
    {
        Move(Path.Combine(staging, SnapshotEntries.Database), paths.DatabaseFile);
        var config = Path.Combine(staging, "config");
        if (Directory.Exists(config))
        {
            Move(config, paths.ConfigDirectory);
        }

        Move(Path.Combine(staging, "keys"), Path.GetDirectoryName(paths.MachineKeyFile)!);
    }

    private static void Move(string source, string target)
    {
        if (Directory.Exists(source))
        {
            Directory.Move(source, target);
        }
        else
        {
            File.Move(source, target);
        }
    }
}
