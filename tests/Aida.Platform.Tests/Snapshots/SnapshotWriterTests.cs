using System.Formats.Tar;
using System.Security.Cryptography;
using Aida.Platform.Keys;
using Aida.Platform.Paths;
using Aida.Platform.Snapshots;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Platform.Tests.Snapshots;

public sealed class SnapshotWriterTests
{
    private const string GoodPassphrase = "frase_valida_de_12";

    [Fact]
    public void Frase_corta_da_Weak_y_con_doce_funciona()
    {
        using var env = new TestEnvironment();
        using var mem = new MemoryStream();
        var ex = Assert.Throws<SnapshotException>(() => env.Writer.Write(mem, "corta_de_11"));
        Assert.Equal(SnapshotError.Weak, ex.Error);

        var count = env.Writer.Write(mem, GoodPassphrase);
        Assert.True(count >= 2);
    }

    [Fact]
    public void Archivo_empieza_por_AIDACOPIA2_y_entradas_en_orden()
    {
        using var env = new TestEnvironment();
        File.WriteAllText(Path.Combine(env.Paths.ConfigDirectory, "app.yaml"), "key: value");

        using var output = new MemoryStream();
        var filesCount = env.Writer.Write(output, GoodPassphrase);
        Assert.Equal(3, filesCount); // aida.db + machine.key + app.yaml

        var bytes = output.ToArray();
        Assert.Equal("AIDACOPIA2\n"u8.ToArray(), bytes[..11]);

        using var readStream = new MemoryStream(bytes);
        using var decrypting = new ChunkDecryptingStream(readStream, GoodPassphrase);
        using var tar = new TarReader(decrypting);

        var entries = new List<string>();
        while (tar.GetNextEntry() is { } entry)
        {
            entries.Add(entry.Name);
        }

        Assert.Equal(4, entries.Count);
        Assert.Equal("manifest.json", entries[0]);
        Assert.Equal("aida.db", entries[1]);
        Assert.Equal("machine.key", entries[2]);
        Assert.Equal("config/app.yaml", entries[3]);
    }

    [Fact]
    public void Archivo_oculto_en_config_no_entra()
    {
        using var env = new TestEnvironment();
        File.WriteAllText(Path.Combine(env.Paths.ConfigDirectory, ".oculto"), "secreto");
        File.WriteAllText(Path.Combine(env.Paths.ConfigDirectory, "visible.yaml"), "publico");

        using var output = new MemoryStream();
        var filesCount = env.Writer.Write(output, GoodPassphrase);
        Assert.Equal(3, filesCount); // aida.db + machine.key + visible.yaml

        output.Position = 0;
        using var decrypting = new ChunkDecryptingStream(output, GoodPassphrase);
        using var tar = new TarReader(decrypting);
        var entries = new List<string>();
        while (tar.GetNextEntry() is { } entry)
        {
            entries.Add(entry.Name);
        }

        Assert.DoesNotContain("config/.oculto", entries);
        Assert.Contains("config/visible.yaml", entries);
    }

    [Fact]
    public void Carpeta_de_paso_queda_limpia_tras_escribir_y_tras_error()
    {
        using var env = new TestEnvironment();
        using var output = new MemoryStream();
        env.Writer.Write(output, GoodPassphrase);

        var files = Directory.GetFiles(env.Paths.TemporaryDirectory);
        Assert.Empty(files);

        // Provocamos un error durante Write (stream que falla)
        using var failing = new FailingStream();
        Assert.Throws<InvalidOperationException>(() => env.Writer.Write(failing, GoodPassphrase));

        var filesAfterError = Directory.GetFiles(env.Paths.TemporaryDirectory);
        Assert.Empty(filesAfterError);
    }

    [Fact]
    public void Base_se_copia_con_wal_activo_y_escrituras_sin_volcar()
    {
        using var env = new TestEnvironment();
        using var conn = env.Database.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS items (v TEXT); INSERT INTO items VALUES ('pendiente');";
            cmd.ExecuteNonQuery();
        }

        // Condición previa: la fila sigue en el WAL; el pool no ha volcado nada en la base.
        Assert.True(new FileInfo($"{env.Paths.DatabaseFile}-wal").Length > 0);

        using var output = new MemoryStream();
        env.Writer.Write(output, GoodPassphrase);

        var extracted = Path.Combine(env.Paths.DataDirectory, "extraida.db");
        output.Position = 0;
        using (var decrypting = new ChunkDecryptingStream(output, GoodPassphrase))
        using (var tar = new TarReader(decrypting))
        {
            while (tar.GetNextEntry() is { } entry)
            {
                if (entry.Name == "aida.db")
                {
                    entry.ExtractToFile(extracted, overwrite: false);
                }
            }
        }

        using var copy = new SqliteConnection($"Data Source={extracted};Mode=ReadOnly;Pooling=False");
        copy.Open();
        using var read = copy.CreateCommand();
        read.CommandText = "SELECT (SELECT v FROM items), (SELECT user_version FROM pragma_user_version);";
        using var reader = read.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("pendiente", reader.GetString(0));
        Assert.Equal(3L, reader.GetInt64(1)); // la versión del esquema de la plataforma (Schema.Version)
    }

    private sealed class FailingStream : MemoryStream
    {
        private int _writes;
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _writes++;
            if (_writes > 2)
            {
                throw new InvalidOperationException("Fallo inducido");
            }
            base.Write(buffer);
        }
    }

    private sealed class TestEnvironment : IDisposable
    {
        private readonly string _dir;
        public ServerPaths Paths { get; }
        public PlatformDatabase Database { get; }
        public MachineKey Key { get; }
        public SnapshotWriter Writer { get; }

        public TestEnvironment()
        {
            _dir = Path.Combine(Path.GetTempPath(), $"aida-test-writer-{RandomNumberGenerator.GetHexString(16)}");
            Directory.CreateDirectory(_dir);
            Paths = new ServerPaths(_dir, _dir);
            Directory.CreateDirectory(Paths.ConfigDirectory);
            Directory.CreateDirectory(Paths.TemporaryDirectory);

            Database = new PlatformDatabase(Paths.DatabaseFile);
            using (var conn = Database.Open())
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE TABLE dummy (id INT); INSERT INTO dummy VALUES (1);";
                cmd.ExecuteNonQuery();
            }

            Key = MachineKey.OpenOrCreate(Paths.MachineKeyFile, new DevelopmentKeyProtector());
            Writer = new SnapshotWriter(Database, Key, Paths, TimeProvider.System);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_dir))
                {
                    Directory.Delete(_dir, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
