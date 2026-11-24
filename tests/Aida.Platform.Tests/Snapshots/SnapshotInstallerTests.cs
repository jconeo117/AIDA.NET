using System.Security.Cryptography;
using System.Text.Json;
using Aida.Platform.Paths;
using Aida.Platform.Snapshots;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Platform.Tests.Snapshots;

public sealed class SnapshotInstallerTests
{
    private static readonly ManualTimeProvider FakeTime = new(new DateTimeOffset(2026, 11, 24, 15, 30, 0, TimeSpan.Zero));

    [Fact]
    public void Sin_restaurar_devuelve_None_sin_tocar_nada()
    {
        using var dir = new TestDirectory();
        var paths = new ServerPaths(dir.Path, dir.Path);

        var result = SnapshotInstaller.ApplyPending(paths, FakeTime);
        Assert.Equal(PendingRestore.None, result);
        Assert.False(File.Exists(paths.RestorePendingFile));
    }

    [Fact]
    public void Sobre_base_sin_usuarios_instala_aparta_lo_anterior_y_crea_aviso()
    {
        using var dir = new TestDirectory();
        var paths = new ServerPaths(dir.Path, dir.Path);
        Directory.CreateDirectory(paths.ConfigDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.MachineKeyFile)!);

        // Archivos existentes previos
        CreateDbWithUsers(paths.DatabaseFile, hasUsers: false);
        File.WriteAllText(Path.Combine(paths.ConfigDirectory, "old.yaml"), "old");
        File.WriteAllBytes(paths.MachineKeyFile, new byte[32]);

        // Lo preparado en restaurar/
        CreateStaged(paths.RestoreStagingDirectory);

        var result = SnapshotInstaller.ApplyPending(paths, FakeTime);
        Assert.Equal(PendingRestore.Installed, result);

        Assert.False(Directory.Exists(paths.RestoreStagingDirectory));
        Assert.True(File.Exists(paths.RestorePendingFile));

        // Lo anterior se movió a antes-20261124-153000
        var antes = Path.Combine(paths.DataDirectory, "antes-20261124-153000");
        Assert.True(Directory.Exists(antes));
        Assert.True(File.Exists(Path.Combine(antes, "aida.db")));
        Assert.True(Directory.Exists(Path.Combine(antes, "config")));
        Assert.True(Directory.Exists(Path.Combine(antes, "keys")));

        // Lo nuevo está en su sitio
        Assert.Equal("staged_config", File.ReadAllText(Path.Combine(paths.ConfigDirectory, "new.yaml")));

        // restore-pending.json con las claves
        var json = File.ReadAllBytes(paths.RestorePendingFile);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("antes-20261124-153000", root.GetProperty("previous").GetString());
        Assert.True(root.TryGetProperty("restored_at", out _));
        Assert.True(root.TryGetProperty("aida_version", out _));
        Assert.True(root.TryGetProperty("created", out _));
        Assert.True(root.TryGetProperty("format", out _));
    }

    [Fact]
    public void Sin_nada_anterior_previous_es_vacio_y_no_hay_carpeta_antes()
    {
        using var dir = new TestDirectory();
        var paths = new ServerPaths(dir.Path, dir.Path);
        CreateStaged(paths.RestoreStagingDirectory);

        var result = SnapshotInstaller.ApplyPending(paths, FakeTime);
        Assert.Equal(PendingRestore.Installed, result);

        var dirsAntes = Directory.GetDirectories(paths.DataDirectory, "antes-*");
        Assert.Empty(dirsAntes);

        using var doc = JsonDocument.Parse(File.ReadAllBytes(paths.RestorePendingFile));
        Assert.Equal(string.Empty, doc.RootElement.GetProperty("previous").GetString());
    }

    [Fact]
    public void Sobre_base_con_usuarios_descarta_y_no_aplica()
    {
        using var dir = new TestDirectory();
        var paths = new ServerPaths(dir.Path, dir.Path);
        CreateDbWithUsers(paths.DatabaseFile, hasUsers: true);
        CreateStaged(paths.RestoreStagingDirectory);

        var result = SnapshotInstaller.ApplyPending(paths, FakeTime);
        Assert.Equal(PendingRestore.Discarded, result);

        Assert.False(Directory.Exists(paths.RestoreStagingDirectory));
        var descartadas = Directory.GetDirectories(paths.DataDirectory, "restaurar.descartada-*");
        Assert.Single(descartadas);
        Assert.False(File.Exists(paths.RestorePendingFile));

        // Base intacta
        AssertDbHasUsers(paths.DatabaseFile);
    }

    [Fact]
    public void Base_ilegible_cuenta_como_sin_usuarios()
    {
        using var dir = new TestDirectory();
        var paths = new ServerPaths(dir.Path, dir.Path);
        File.WriteAllText(paths.DatabaseFile, "archivo_danado");
        CreateStaged(paths.RestoreStagingDirectory);

        var result = SnapshotInstaller.ApplyPending(paths, FakeTime);
        Assert.Equal(PendingRestore.Installed, result);
    }

    private static void CreateStaged(string stagingDir)
    {
        Directory.CreateDirectory(stagingDir);
        Directory.CreateDirectory(Path.Combine(stagingDir, "config"));
        Directory.CreateDirectory(Path.Combine(stagingDir, "keys"));

        CreateDbWithUsers(Path.Combine(stagingDir, "aida.db"), hasUsers: false);
        File.WriteAllText(Path.Combine(stagingDir, "config", "new.yaml"), "staged_config");
        File.WriteAllBytes(Path.Combine(stagingDir, "keys", "machine.key"), new byte[32]);

        var notice = new Dictionary<string, string>
        {
            ["aida_version"] = "1.0",
            ["created"] = "2026-11-24T10:00:00Z",
            ["files"] = "3",
            ["format"] = "1",
        };
        File.WriteAllBytes(Path.Combine(stagingDir, "notice.json"), SnapshotManifest.Serialize(notice));
    }

    private static void CreateDbWithUsers(string path, bool hasUsers)
    {
        using var conn = new SqliteConnection($"Data Source={path};Mode=ReadWriteCreate;Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE users (id TEXT PRIMARY KEY);";
        if (hasUsers)
        {
            cmd.CommandText += " INSERT INTO users VALUES ('admin');";
        }
        cmd.ExecuteNonQuery();
    }

    private static void AssertDbHasUsers(string path)
    {
        using var conn = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM users;";
        Assert.Equal(1L, (long)cmd.ExecuteScalar()!);
    }

    private sealed class ManualTimeProvider(DateTimeOffset time) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => time;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class TestDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aida-inst-{RandomNumberGenerator.GetHexString(12)}");
        public TestDirectory() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
