using System.Formats.Tar;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Aida.Platform.Keys;
using Aida.Platform.Paths;

namespace Aida.Platform.Snapshots;

/// <summary>Valida una copia y la deja preparada para el siguiente arranque.</summary>
/// <remarks>
/// <para>Todo se valida antes de que la carpeta preparada exista: se descifra en una carpeta de paso, y solo si todo
/// cuadra se renombra a <see cref="ServerPaths.RestoreStagingDirectory"/>. Una copia rota o manipulada no deja nada
/// (<c>snapshot.py:3-9</c> de la 0.1 lo pedía igual).</para>
/// <para>La clave se escribe ya envuelta con el protector de este equipo: en el disco nunca está en claro (ADR 0034).
/// No se aplica aquí porque la base está abierta y la clave en uso; la aplica <see cref="SnapshotInstaller"/> al
/// arrancar (ADR 0036).</para>
/// </remarks>
public sealed class SnapshotStager
{
    /// <summary>El tamaño máximo del archivo que se sube: 1 GiB de contenido y lo que añade el cifrado.</summary>
    public const long MaxFileBytes = (1L << 30) + (1L << 20);

    private readonly object _gate = new();
    private readonly ServerPaths _paths;
    private readonly IKeyProtector _protector;
    private readonly IReadOnlyDictionary<string, int> _armSchemaVersions;

    /// <summary>Crea el preparador.</summary>
    /// <param name="paths">Las rutas.</param>
    /// <param name="protector">El protector de la clave en este equipo.</param>
    /// <param name="armSchemaVersions">La versión de esquema que conoce este AIDA de cada brazo.</param>
    public SnapshotStager(ServerPaths paths, IKeyProtector protector, IReadOnlyDictionary<string, int> armSchemaVersions)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _armSchemaVersions = armSchemaVersions ?? throw new ArgumentNullException(nameof(armSchemaVersions));
    }

    /// <summary>Valida la copia y la deja preparada.</summary>
    /// <param name="file">El archivo subido.</param>
    /// <param name="passphrase">La frase de la copia.</param>
    /// <exception cref="SnapshotException">Con la razón; en ese caso no queda nada preparado.</exception>
    /// <remarks>Con un candado de instancia: dos subidas a la vez no se pisan la carpeta preparada.</remarks>
    public void Stage(Stream file, string passphrase)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(passphrase);
        lock (_gate)
        {
            var staging = Path.Combine(_paths.DataDirectory, $"restaurar.tmp-{RandomNumberGenerator.GetHexString(32, lowercase: true)}");
            Directory.CreateDirectory(staging);
            try
            {
                var manifest = Extract(file, passphrase, staging);
                StagedDatabase.Check(Path.Combine(staging, SnapshotEntries.Database), _armSchemaVersions);
                File.WriteAllBytes(Path.Combine(staging, "notice.json"), SnapshotManifest.Serialize(manifest));
                if (Directory.Exists(_paths.RestoreStagingDirectory))
                {
                    Directory.Delete(_paths.RestoreStagingDirectory, recursive: true);
                }

                Directory.Move(staging, _paths.RestoreStagingDirectory);
            }
            catch (Exception exception) when (exception is InvalidDataException or FormatException or EndOfStreamException or JsonException)
            {
                Discard(staging);
                throw new SnapshotException(SnapshotError.BadFile, exception);
            }
            catch
            {
                Discard(staging);
                throw;
            }
        }
    }

    private IReadOnlyDictionary<string, string> Extract(Stream file, string passphrase, string staging)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyDictionary<string, string>? manifest = null;
        using var decrypting = new ChunkDecryptingStream(file, passphrase);
        using var tar = new TarReader(decrypting, leaveOpen: true);
        while (tar.GetNextEntry() is { } entry)
        {
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            {
                throw new SnapshotException(SnapshotError.UnsafePath);
            }

            if (!SnapshotEntries.IsAllowed(entry.Name) || !names.Add(entry.Name))
            {
                throw new SnapshotException(SnapshotError.UnsafePath);
            }

            if (names.Count == 1 && entry.Name != SnapshotEntries.Manifest)
            {
                throw new SnapshotException(SnapshotError.BadFile);
            }

            if (entry.Name == SnapshotEntries.Manifest)
            {
                manifest = SnapshotManifest.ReadEntry(entry);
            }
            else
            {
                Save(entry, staging);
            }
        }

        decrypting.CopyTo(Stream.Null);
        if (manifest is null || !names.Contains(SnapshotEntries.Database) || !names.Contains(SnapshotEntries.Key))
        {
            throw new SnapshotException(SnapshotError.BadFile);
        }

        return manifest["files"] == (names.Count - 1).ToString(CultureInfo.InvariantCulture)
            ? manifest
            : throw new SnapshotException(SnapshotError.BadFile);
    }

    private void Save(TarEntry entry, string staging)
    {
        if (entry.Name == SnapshotEntries.Key)
        {
            SaveKey(entry, staging);
            return;
        }

        var target = entry.Name == SnapshotEntries.Database
            ? Path.Combine(staging, SnapshotEntries.Database)
            : Path.Combine(staging, entry.Name);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write);
        entry.DataStream?.CopyTo(output);
    }

    private void SaveKey(TarEntry entry, string staging)
    {
        if (entry.Length != MachineKey.Length || entry.DataStream is null)
        {
            throw new SnapshotException(SnapshotError.BadFile);
        }

        var raw = new byte[MachineKey.Length];
        try
        {
            entry.DataStream.ReadExactly(raw);
            MachineKey.WriteNew(Path.Combine(staging, "keys", "machine.key"), raw, _protector);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(raw);
        }
    }

    private static void Discard(string staging)
    {
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }
    }
}
