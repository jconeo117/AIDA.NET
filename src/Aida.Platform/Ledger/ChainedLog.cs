using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Aida.Platform.Ledger;

/// <summary>Un registro solo de añadir y encadenado: cada línea lleva la huella de la anterior.</summary>
/// <remarks>
/// Es <c>platform/ledger.py</c> de la 0.1 sobre la tabla <c>ledger</c>: la misma huella (SHA-256 del JSON de
/// <see cref="LedgerJson"/>), la misma génesis y las mismas tres clases de problema. No impide reescribir la tabla
/// entera, pero un cambio parcial no pasa desapercibido. La línea se encadena dentro de la transacción de quien
/// escribe: dos sesiones a la vez nunca encadenan detrás de la misma huella.
/// </remarks>
internal static class ChainedLog
{
    internal const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000";

    internal static string Digest(IEnumerable<KeyValuePair<string, string>> fields) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(LedgerJson.Write(fields))));

    internal static string Append(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyDictionary<string, string> fields)
    {
        if (fields.ContainsKey("prev") || fields.ContainsKey("digest"))
        {
            throw new ArgumentException("'prev' y 'digest' los pone la cadena, no quien añade", nameof(fields));
        }

        var line = new Dictionary<string, string>(fields, StringComparer.Ordinal) { ["prev"] = LastDigest(connection, transaction) };
        var digest = Digest(line);
        line["digest"] = digest;
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO ledger (digest, line) VALUES ($digest, $line);";
        insert.Parameters.AddWithValue("$digest", digest);
        insert.Parameters.AddWithValue("$line", LedgerJson.Write(line));
        insert.ExecuteNonQuery();
        return digest;
    }

    internal static IReadOnlyList<string> Verify(SqliteConnection connection)
    {
        var problems = new List<string>();
        var previous = Genesis;
        var number = 0;
        foreach (var text in Lines(connection))
        {
            number++;
            var fields = TryParse(text);
            if (fields is null || !fields.Remove("digest", out var stored))
            {
                problems.Add($"línea {number}: no se puede leer");
                previous = string.Empty;
                continue;
            }

            if (!fields.TryGetValue("prev", out var prev) || prev != previous)
            {
                problems.Add($"línea {number}: no sigue a la anterior");
            }

            if (Digest(fields) != stored)
            {
                problems.Add($"línea {number}: su contenido no corresponde a su huella");
            }

            previous = stored;
        }

        return problems;
    }

    internal static IReadOnlyList<string> Lines(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT line FROM ledger ORDER BY seq;";
        using var reader = select.ExecuteReader();
        var lines = new List<string>();
        while (reader.Read())
        {
            lines.Add(reader.GetString(0));
        }

        return lines;
    }

    private static string LastDigest(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT digest FROM ledger ORDER BY seq DESC LIMIT 1;";
        return select.ExecuteScalar() as string ?? Genesis;
    }

    private static Dictionary<string, string>? TryParse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                fields[property.Name] = property.Value.GetString()!;
            }

            return fields;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
