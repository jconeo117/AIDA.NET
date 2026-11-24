using System.Formats.Tar;
using System.Globalization;
using System.Security.Cryptography;
using Aida.Platform.Keys;
using Aida.Platform.Paths;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;

namespace Aida.Platform.Snapshots;

/// <summary>Hace la copia: la base, la configuración y la clave del equipo, cifradas con una frase.</summary>
/// <remarks>
/// <para>Es <c>create_snapshot</c> de la 0.1 (<c>snapshot.py:69-90</c>), con dos diferencias (ADR 0036): entra la base
/// entera, en una copia coherente con la API de copia de SQLite, y entra la clave **en bruto**, porque el archivo
/// <c>machine.key</c> va envuelto con DPAPI y no serviría en otro equipo.</para>
/// <para>Quien tiene el archivo y la frase lo puede leer todo. Por eso pide un permiso propio y queda en la auditoría.</para>
/// </remarks>
public sealed class SnapshotWriter
{
    private readonly PlatformDatabase _database;
    private readonly MachineKey _key;
    private readonly ServerPaths _paths;
    private readonly TimeProvider _time;

    /// <summary>Crea el escritor.</summary>
    /// <param name="database">La base que se copia.</param>
    /// <param name="key">La clave del equipo.</param>
    /// <param name="paths">Las rutas: la configuración y la carpeta de paso.</param>
    /// <param name="time">El reloj del manifiesto.</param>
    public SnapshotWriter(PlatformDatabase database, MachineKey key, ServerPaths paths, TimeProvider time)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>Escribe la copia cifrada.</summary>
    /// <param name="output">Adónde se escribe. No se cierra.</param>
    /// <param name="passphrase">La frase, de 12 caracteres o más.</param>
    /// <returns>Cuántos archivos lleva, sin contar el manifiesto.</returns>
    /// <exception cref="SnapshotException"><see cref="SnapshotError.Weak"/> si la frase es corta;
    /// <see cref="SnapshotError.TooLarge"/> si pasa de 1 GiB.</exception>
    /// <remarks>La copia de la base se borra al terminar, también tras un error: no queda ninguna copia en claro en la carpeta de paso.</remarks>
    public int Write(Stream output, string passphrase)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (passphrase is null || passphrase.Length < SnapshotFormat.MinPassphrase)
        {
            throw new SnapshotException(SnapshotError.Weak);
        }

        Directory.CreateDirectory(_paths.TemporaryDirectory);
        var copy = Path.Combine(_paths.TemporaryDirectory, $"{RandomNumberGenerator.GetHexString(32, lowercase: true)}.db");
        try
        {
            CopyDatabase(copy);
            var configFiles = ConfigFiles();
            var total = new FileInfo(copy).Length + MachineKey.Length + configFiles.Sum(path => new FileInfo(path).Length);
            if (total > SnapshotFormat.MaxBytes)
            {
                throw new SnapshotException(SnapshotError.TooLarge);
            }

            var files = 2 + configFiles.Count;
            using var tar = new TarWriter(new ChunkEncryptingStream(output, passphrase), TarEntryFormat.Ustar);
            WriteManifest(tar, files);
            using (var database = File.OpenRead(copy))
            {
                Add(tar, SnapshotEntries.Database, database);
            }

            WriteKey(tar);
            foreach (var path in configFiles)
            {
                using var data = File.OpenRead(path);
                Add(tar, SnapshotEntries.ConfigPrefix + new FileInfo(path).Name, data);
            }

            return files;
        }
        finally
        {
            DeleteCopy(copy);
        }
    }

    private void CopyDatabase(string destinationFile)
    {
        using var source = _database.Open();
        var text = new SqliteConnectionStringBuilder
        {
            DataSource = destinationFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();
        using var destination = new SqliteConnection(text);
        destination.Open();
        typeof(SqliteConnection)
            .GetMethods()
            .First(method => method.Name.EndsWith("upDatabase", StringComparison.Ordinal) && method.GetParameters().Length == 1)
            .Invoke(source, [destination]);
    }

    private IReadOnlyList<string> ConfigFiles()
    {
        if (!Directory.Exists(_paths.ConfigDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(_paths.ConfigDirectory)
            .Where(path => SnapshotEntries.IsAllowed(SnapshotEntries.ConfigPrefix + new FileInfo(path).Name))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private void WriteManifest(TarWriter tar, int files)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["aida_version"] = SnapshotManifest.AidaVersion(),
            ["created"] = SnapshotManifest.Timestamp(_time.GetLocalNow()),
            ["files"] = files.ToString(CultureInfo.InvariantCulture),
            ["format"] = SnapshotManifest.Format,
        };
        using var manifest = new MemoryStream(SnapshotManifest.Serialize(values));
        Add(tar, SnapshotEntries.Manifest, manifest);
    }

    private void WriteKey(TarWriter tar)
    {
        var raw = _key.Export();
        try
        {
            using var stream = new MemoryStream(raw);
            Add(tar, SnapshotEntries.Key, stream);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(raw);
        }
    }

    private static void Add(TarWriter tar, string name, Stream data) =>
        tar.WriteEntry(new UstarTarEntry(TarEntryType.RegularFile, name) { DataStream = data });

    private static void DeleteCopy(string copy)
    {
        File.Delete(copy);
        File.Delete($"{copy}-wal");
        File.Delete($"{copy}-shm");
    }
}
