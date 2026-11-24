using System.Text.Json;
using Aida.Platform.Audit;
using Aida.Platform.Paths;

namespace Aida.Platform.Snapshots;

/// <summary>El aviso de una copia aplicada: lo firma el primero que entra.</summary>
/// <remarks>
/// La auditoría exige autor (ADR 0006), y al aplicar al arrancar no hay nadie. La 0.1 lo resolvía igual
/// (<c>shell.py:225-229</c>): el primero que se identifica firma <c>snapshot_restored</c> con los datos del aviso.
/// </remarks>
public sealed class RestoreNotice
{
    private readonly ServerPaths _paths;
    private readonly AuditLog _audit;
    private readonly object _gate = new();

    /// <summary>Crea el aviso.</summary>
    /// <param name="paths">Dónde está el archivo del aviso.</param>
    /// <param name="audit">La auditoría.</param>
    public RestoreNotice(ServerPaths paths, AuditLog audit)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    /// <summary>Si hay un aviso, lo firma y lo borra.</summary>
    /// <param name="actor">Quién acaba de entrar.</param>
    /// <param name="workstation">Desde qué puesto.</param>
    /// <remarks>Con un candado de instancia: dos entradas a la vez firman una sola vez. Primero se firma y después se borra:
    /// si se cae entre las dos, el aviso se firma otra vez, y nunca se pierde.</remarks>
    public void SignIfPending(Actor actor, string workstation)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(workstation);
        lock (_gate)
        {
            if (!File.Exists(_paths.RestorePendingFile))
            {
                return;
            }

            _audit.Record(AuditKind.SnapshotRestored, actor, workstation, ReadPending(_paths.RestorePendingFile));
            File.Delete(_paths.RestorePendingFile);
        }
    }

    private static IReadOnlyDictionary<string, string> ReadPending(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            foreach (var key in SnapshotManifest.PendingKeys)
            {
                if (document.RootElement.TryGetProperty(key, out var element) && element.ValueKind == JsonValueKind.String)
                {
                    values[key] = element.GetString() ?? string.Empty;
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            values.Clear();
        }

        return SnapshotManifest.PendingKeys.ToDictionary(key => key, key => values.GetValueOrDefault(key, string.Empty), StringComparer.Ordinal);
    }
}
