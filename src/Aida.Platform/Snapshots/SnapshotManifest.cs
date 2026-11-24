using System.Formats.Tar;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Aida.Platform.Snapshots;

/// <summary>Escribe y lee el manifiesto de una copia, y el aviso que deja la restauración.</summary>
/// <remarks>
/// El manifiesto va primero en la copia y dice qué versión la hizo y cuántas entradas trae, para que la restauración
/// rechace una copia cuyas entradas no cuadran. Las claves van ordenadas y sin espacios: el mismo contenido sale siempre
/// igual, y se puede comparar a ojo (ADR 0036).
/// </remarks>
internal static class SnapshotManifest
{
    internal const string Format = "1";
    internal const int MaxBytes = 64 * 1024;

    /// <summary>Las claves del manifiesto, que también son las de <c>notice.json</c>.</summary>
    internal static IReadOnlyList<string> CopyKeys { get; } = ["aida_version", "created", "files", "format"];

    /// <summary>Las claves del aviso de una restauración aplicada: las de la copia, más lo que pasó al aplicarla.</summary>
    internal static IReadOnlyList<string> PendingKeys { get; } =
        ["aida_version", "created", "files", "format", "previous", "restored_at"];

    /// <summary>La versión de AIDA que escribe las copias.</summary>
    /// <returns>La versión informativa de <c>Aida.Platform</c>, o <c>"0"</c> si no la tiene.</returns>
    internal static string AidaVersion() =>
        typeof(SnapshotManifest).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0";

    /// <summary>Un instante como lo escribe la auditoría: hora local con su desfase, como <c>AuditLog</c>.</summary>
    /// <param name="moment">El instante.</param>
    /// <returns>El texto del instante.</returns>
    internal static string Timestamp(DateTimeOffset moment) =>
        moment.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    /// <summary>Escribe un objeto JSON con sus valores de texto, en orden ordinal y sin espacios.</summary>
    /// <param name="values">Las claves y sus valores.</param>
    /// <returns>El JSON en UTF-8.</returns>
    internal static byte[] Serialize(IEnumerable<KeyValuePair<string, string>> values)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            foreach (var pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteString(pair.Key, pair.Value);
            }

            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    /// <summary>Lee el manifiesto de la copia, con su límite de tamaño.</summary>
    /// <param name="entry">La entrada <c>manifest.json</c>.</param>
    /// <returns>Las cuatro claves de <see cref="CopyKeys"/>.</returns>
    /// <exception cref="SnapshotException"><see cref="SnapshotError.BadFile"/> si pesa más de 64 KiB o no tiene la forma.</exception>
    /// <remarks>El tamaño se mira antes de leer: un manifiesto desmesurado no se carga en memoria.</remarks>
    internal static IReadOnlyDictionary<string, string> ReadEntry(TarEntry entry)
    {
        if (entry.Length > MaxBytes)
        {
            throw new SnapshotException(SnapshotError.BadFile);
        }

        using var memory = new MemoryStream();
        entry.DataStream?.CopyTo(memory);
        return Parse(memory.ToArray());
    }

    /// <summary>Lee un manifiesto o un aviso ya en disco, y comprueba que tiene las cuatro claves de la copia.</summary>
    /// <param name="json">El JSON en UTF-8.</param>
    /// <returns>Las cuatro claves, como texto.</returns>
    /// <exception cref="SnapshotException"><see cref="SnapshotError.BadFile"/> si no es un objeto con esas claves, o <c>format</c> no es <c>"1"</c>.</exception>
    /// <exception cref="JsonException">Si el texto no es JSON.</exception>
    internal static IReadOnlyDictionary<string, string> Parse(byte[] json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new SnapshotException(SnapshotError.BadFile);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in CopyKeys)
        {
            if (!root.TryGetProperty(key, out var element) || element.ValueKind != JsonValueKind.String || element.GetString() is not { } text)
            {
                throw new SnapshotException(SnapshotError.BadFile);
            }

            values[key] = text;
        }

        return values["format"] == Format
            ? values
            : throw new SnapshotException(SnapshotError.BadFile);
    }
}
