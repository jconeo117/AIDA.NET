using System.Formats.Tar;
using System.Security.Cryptography;
using Aida.Platform.Keys;
using Aida.Platform.Paths;
using Aida.Platform.Snapshots;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Platform.Tests.Snapshots;

public sealed class SnapshotStagerTests
{
    private const string Passphrase = "frase_segura_para_stager_12";

    [Fact]
    public void Ida_y_vuelta_entre_servidores_con_otro_protector()
    {
        using var envA = new ServerEnv("server-a", new DummyProtector((byte)0xAA));
        using var envB = new ServerEnv("server-b", new DummyProtector((byte)0xBB));

        using (var conn = envA.Database.Open())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE test_data (id INT, name TEXT); INSERT INTO test_data VALUES (1, 'Admin');";
            cmd.ExecuteNonQuery();
        }
        File.WriteAllText(Path.Combine(envA.Paths.ConfigDirectory, "settings.json"), "{\"app\":1}");

        using var copyStream = new MemoryStream();
        envA.Writer.Write(copyStream, Passphrase);
        copyStream.Position = 0;

        var stagerB = new SnapshotStager(envB.Paths, envB.Protector, new Dictionary<string, int>());
        stagerB.Stage(copyStream, Passphrase);

        Assert.True(Directory.Exists(envB.Paths.RestoreStagingDirectory));
        var stagedDb = Path.Combine(envB.Paths.RestoreStagingDirectory, "aida.db");
        Assert.True(File.Exists(stagedDb));
        using (var connB = new SqliteConnection($"Data Source={stagedDb};Mode=ReadOnly;Pooling=False"))
        {
            connB.Open();
            using var cmd = connB.CreateCommand();
            cmd.CommandText = "SELECT name FROM test_data WHERE id = 1;";
            Assert.Equal("Admin", cmd.ExecuteScalar());
        }

        var stagedConfig = Path.Combine(envB.Paths.RestoreStagingDirectory, "config", "settings.json");
        Assert.Equal("{\"app\":1}", File.ReadAllText(stagedConfig));

        var stagedKey = Path.Combine(envB.Paths.RestoreStagingDirectory, "keys", "machine.key");
        var protectedBytes = File.ReadAllBytes(stagedKey);
        var rawFromA = envA.Key.Export();
        Assert.False(protectedBytes.SequenceEqual(rawFromA)); // no contiene raw
        var unprotected = envB.Protector.Unprotect(protectedBytes);
        Assert.Equal(rawFromA, unprotected);
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("/x")]
    [InlineData("config/a/b")]
    [InlineData("keys/machine.key")]
    public void Entradas_prohibidas_dan_UnsafePath_y_limpian_temporales(string unsafeEntry)
    {
        using var env = new ServerEnv("test-unsafe", new DevelopmentKeyProtector());
        using var tarStream = CreateTar(builder =>
        {
            builder.AddManifest(2);
            builder.AddDatabase();
            builder.AddKey();
            builder.AddEntry(unsafeEntry, "contenido"u8.ToArray());
        });

        var stager = new SnapshotStager(env.Paths, env.Protector, new Dictionary<string, int>());
        var ex = Assert.Throws<SnapshotException>(() => stager.Stage(tarStream, Passphrase));
        Assert.Equal(SnapshotError.UnsafePath, ex.Error);

        AssertNoStagingOrTemp(env.Paths);
    }

    [Fact]
    public void Entrada_simbolica_o_repetida_dan_UnsafePath()
    {
        using var env = new ServerEnv("test-symlink-dup", new DevelopmentKeyProtector());

        // Base repetida
        using var dupStream = CreateTar(builder =>
        {
            builder.AddManifest(2);
            builder.AddDatabase();
            builder.AddKey();
            builder.AddEntry("aida.db", "repetida"u8.ToArray());
        });
        var stager = new SnapshotStager(env.Paths, env.Protector, new Dictionary<string, int>());
        var exDup = Assert.Throws<SnapshotException>(() => stager.Stage(dupStream, Passphrase));
        Assert.Equal(SnapshotError.UnsafePath, exDup.Error);
        AssertNoStagingOrTemp(env.Paths);

        // Enlace simbólico
        using var symStream = CreateTar(builder =>
        {
            builder.AddManifest(2);
            builder.AddDatabase();
            builder.AddKey();
            builder.AddTarEntry(new PaxTarEntry(TarEntryType.SymbolicLink, "config/link") { LinkName = "/etc/passwd" });
        });
        var exSym = Assert.Throws<SnapshotException>(() => stager.Stage(symStream, Passphrase));
        Assert.Equal(SnapshotError.UnsafePath, exSym.Error);
        AssertNoStagingOrTemp(env.Paths);
    }

    [Fact]
    public void Fallos_de_estructura_dan_BadFile()
    {
        using var env = new ServerEnv("test-badfile", new DevelopmentKeyProtector());
        var stager = new SnapshotStager(env.Paths, env.Protector, new Dictionary<string, int>());

        // Manifiesto no primero
        using var s1 = CreateTar(b => { b.AddDatabase(); b.AddManifest(2); b.AddKey(); });
        Assert.Equal(SnapshotError.BadFile, Assert.Throws<SnapshotException>(() => stager.Stage(s1, Passphrase)).Error);

        // Falta machine.key
        using var s2 = CreateTar(b => { b.AddManifest(1); b.AddDatabase(); });
        Assert.Equal(SnapshotError.BadFile, Assert.Throws<SnapshotException>(() => stager.Stage(s2, Passphrase)).Error);

        // Clave de 31 bytes
        using var s3 = CreateTar(b => { b.AddManifest(2); b.AddDatabase(); b.AddEntry("machine.key", new byte[31]); });
        Assert.Equal(SnapshotError.BadFile, Assert.Throws<SnapshotException>(() => stager.Stage(s3, Passphrase)).Error);

        // format != 1
        using var s4 = CreateTar(b => { b.AddCustomManifest("{\"format\":\"2\",\"files\":\"2\",\"aida_version\":\"0\",\"created\":\"\"}"); b.AddDatabase(); b.AddKey(); });
        Assert.Equal(SnapshotError.BadFile, Assert.Throws<SnapshotException>(() => stager.Stage(s4, Passphrase)).Error);

        // files count mismatch
        using var s5 = CreateTar(b => { b.AddManifest(5); b.AddDatabase(); b.AddKey(); });
        Assert.Equal(SnapshotError.BadFile, Assert.Throws<SnapshotException>(() => stager.Stage(s5, Passphrase)).Error);

        // Base corrupta
        using var s6 = CreateTar(b => { b.AddManifest(2); b.AddEntry("aida.db", "no es sqlite"u8.ToArray()); b.AddKey(); });
        Assert.Equal(SnapshotError.BadFile, Assert.Throws<SnapshotException>(() => stager.Stage(s6, Passphrase)).Error);

        AssertNoStagingOrTemp(env.Paths);
    }

    [Fact]
    public void Esquema_mas_nuevo_da_TooNew_y_brazo_desconocido_se_ignora()
    {
        using var env = new ServerEnv("test-version", new DevelopmentKeyProtector());

        // user_version = 3 (mayor a Schema.Version = 2)
        using var sTooNew = CreateTar(b =>
        {
            b.AddManifest(2);
            b.AddCustomDatabase(conn => { using var c = conn.CreateCommand(); c.CommandText = "PRAGMA user_version = 3;"; c.ExecuteNonQuery(); });
            b.AddKey();
        });
        var stager = new SnapshotStager(env.Paths, env.Protector, new Dictionary<string, int>());
        Assert.Equal(SnapshotError.TooNew, Assert.Throws<SnapshotException>(() => stager.Stage(sTooNew, Passphrase)).Error);
        AssertNoStagingOrTemp(env.Paths);

        // arm_schema mayor que el conocido
        using var sArmNew = CreateTar(b =>
        {
            b.AddManifest(2);
            b.AddCustomDatabase(conn =>
            {
                using var c = conn.CreateCommand();
                c.CommandText = "CREATE TABLE arm_schema (arm TEXT PRIMARY KEY, version INT); INSERT INTO arm_schema VALUES ('agenda', 5);";
                c.ExecuteNonQuery();
            });
            b.AddKey();
        });
        var stagerKnown = new SnapshotStager(env.Paths, env.Protector, new Dictionary<string, int> { ["agenda"] = 2 });
        Assert.Equal(SnapshotError.TooNew, Assert.Throws<SnapshotException>(() => stagerKnown.Stage(sArmNew, Passphrase)).Error);
        AssertNoStagingOrTemp(env.Paths);

        // brazo desconocido se ignora
        using var sArmUnknown = CreateTar(b =>
        {
            b.AddManifest(2);
            b.AddCustomDatabase(conn =>
            {
                using var c = conn.CreateCommand();
                c.CommandText = "CREATE TABLE arm_schema (arm TEXT PRIMARY KEY, version INT); INSERT INTO arm_schema VALUES ('brazo_raro', 99);";
                c.ExecuteNonQuery();
            });
            b.AddKey();
        });
        stagerKnown.Stage(sArmUnknown, Passphrase);
        Assert.True(Directory.Exists(env.Paths.RestoreStagingDirectory));
    }

    private static void AssertNoStagingOrTemp(ServerPaths paths)
    {
        Assert.False(Directory.Exists(paths.RestoreStagingDirectory));
        var tmps = Directory.GetDirectories(paths.DataDirectory, "restaurar.tmp-*");
        Assert.Empty(tmps);
    }

    private static Stream CreateTar(Action<TarBuilder> build)
    {
        var mem = new MemoryStream();
        using (var enc = new ChunkEncryptingStream(mem, Passphrase))
        using (var tar = new TarWriter(enc, TarEntryFormat.Ustar, leaveOpen: true))
        {
            var builder = new TarBuilder(tar);
            build(builder);
        }
        mem.Position = 0;
        return mem;
    }

    private sealed class TarBuilder(TarWriter writer)
    {
        public void AddManifest(int files) =>
            AddEntry("manifest.json", System.Text.Encoding.UTF8.GetBytes($"{{\"aida_version\":\"0\",\"created\":\"2026-11-24T00:00:00Z\",\"files\":\"{files}\",\"format\":\"1\"}}"));

        public void AddCustomManifest(string json) =>
            AddEntry("manifest.json", System.Text.Encoding.UTF8.GetBytes(json));

        public void AddDatabase()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                using (var conn = new SqliteConnection($"Data Source={tmp};Pooling=False"))
                {
                    conn.Open();
                    using var c = conn.CreateCommand();
                    c.CommandText = "CREATE TABLE t (x INT); PRAGMA user_version = 2;";
                    c.ExecuteNonQuery();
                }
                AddEntry("aida.db", File.ReadAllBytes(tmp));
            }
            finally { File.Delete(tmp); }
        }

        public void AddCustomDatabase(Action<SqliteConnection> setup)
        {
            var tmp = Path.GetTempFileName();
            try
            {
                using (var conn = new SqliteConnection($"Data Source={tmp};Pooling=False"))
                {
                    conn.Open();
                    setup(conn);
                }
                AddEntry("aida.db", File.ReadAllBytes(tmp));
            }
            finally { File.Delete(tmp); }
        }

        public void AddKey() => AddEntry("machine.key", new byte[32]);

        public void AddEntry(string name, byte[] data)
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(data) };
            writer.WriteEntry(entry);
        }

        public void AddTarEntry(TarEntry entry) => writer.WriteEntry(entry);
    }

    private sealed class DummyProtector(byte mask) : IKeyProtector
    {
        public byte[] Protect(byte[] raw) => raw.Select(b => (byte)(b ^ mask)).ToArray();
        public byte[] Unprotect(byte[] wrapped) => wrapped.Select(b => (byte)(b ^ mask)).ToArray();
    }

    private sealed class ServerEnv : IDisposable
    {
        private readonly string _dir;
        public ServerPaths Paths { get; }
        public IKeyProtector Protector { get; }
        public PlatformDatabase Database { get; }
        public MachineKey Key { get; }
        public SnapshotWriter Writer { get; }

        public ServerEnv(string tag, IKeyProtector protector)
        {
            _dir = Path.Combine(Path.GetTempPath(), $"aida-env-{tag}-{RandomNumberGenerator.GetHexString(12)}");
            Directory.CreateDirectory(_dir);
            Paths = new ServerPaths(_dir, _dir);
            Directory.CreateDirectory(Paths.ConfigDirectory);
            Protector = protector;
            Database = new PlatformDatabase(Paths.DatabaseFile);
            Key = MachineKey.OpenOrCreate(Paths.MachineKeyFile, Protector);
            Writer = new SnapshotWriter(Database, Key, Paths, TimeProvider.System);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }
    }
}
