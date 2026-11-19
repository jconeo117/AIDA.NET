using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Aida.Platform.Storage;

/// <summary>La versión del esquema de cada brazo, en la tabla <c>arm_schema</c>.</summary>
/// <remarks>Cada brazo versiona sus tablas por separado (ADR 0032): así un brazo nuevo no obliga a subir la versión de la
/// plataforma, y dos brazos no se pisan. Igual que la plataforma, un archivo más nuevo que el código no se abre.</remarks>
internal static partial class ArmSchema
{
    internal static void Apply(SqliteConnection connection, string armId, IReadOnlyList<string> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (armId is null || !IdPattern().IsMatch(armId))
        {
            throw new ArgumentException($"«{armId}» no es un identificador de brazo válido", nameof(armId));
        }

        if (steps.Count == 0)
        {
            throw new ArgumentException("un esquema de brazo tiene al menos un paso", nameof(steps));
        }

        using var transaction = connection.BeginTransaction(deferred: false);
        long current;
        using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT version FROM arm_schema WHERE arm = $arm;";
            read.Parameters.AddWithValue("$arm", armId);
            current = read.ExecuteScalar() is long stored ? stored : 0;
        }

        if (current > steps.Count)
        {
            throw new InvalidOperationException($"el esquema del brazo «{armId}» es de la versión {current} y este AIDA solo conoce hasta la {steps.Count}");
        }

        if (current < steps.Count)
        {
            using var apply = connection.CreateCommand();
            apply.Transaction = transaction;
            apply.CommandText = string.Concat(steps.Skip((int)current));
            apply.ExecuteNonQuery();
            using var write = connection.CreateCommand();
            write.Transaction = transaction;
            write.CommandText = "INSERT INTO arm_schema (arm, version) VALUES ($arm, $version) ON CONFLICT (arm) DO UPDATE SET version = excluded.version;";
            write.Parameters.AddWithValue("$arm", armId);
            write.Parameters.AddWithValue("$version", (long)steps.Count);
            write.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
