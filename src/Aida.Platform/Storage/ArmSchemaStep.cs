using Microsoft.Data.Sqlite;

namespace Aida.Platform.Storage;

/// <summary>Un paso del esquema de un brazo: SQL, o código cuando hay que mover datos que el SQL no puede mover.</summary>
/// <remarks>ADR 0038, que precisa el 0032. Los pasos se aplican en orden y en una sola transacción: si uno falla, no queda
/// nada a medias, ni el SQL de los pasos anteriores ni la versión nueva. Por eso el código recibe la transacción de los pasos
/// y no abre la suya: un paso que confirmara por su cuenta dejaría el esquema a medias si el siguiente fallara.</remarks>
public sealed class ArmSchemaStep
{
    private readonly Action<SqliteConnection, SqliteTransaction> _run;

    private ArmSchemaStep(Action<SqliteConnection, SqliteTransaction> run) => _run = run;

    /// <summary>Un paso de SQL.</summary>
    /// <param name="sql">El SQL del paso; puede tener varias sentencias.</param>
    /// <returns>El paso.</returns>
    /// <exception cref="ArgumentException">Si el SQL está vacío o solo tiene espacios.</exception>
    public static ArmSchemaStep Sql(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        return new ArmSchemaStep((connection, transaction) => ExecuteSql(connection, transaction, sql));
    }

    /// <summary>Un paso de código.</summary>
    /// <param name="apply">Lo que hace el paso, con la conexión y la transacción del esquema. Si lanza, el paso y los que
    /// vienen detrás no se confirman.</param>
    /// <returns>El paso.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="apply"/> es <see langword="null"/>.</exception>
    public static ArmSchemaStep Code(Action<SqliteConnection, SqliteTransaction> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        return new ArmSchemaStep(apply);
    }

    /// <summary>Aplica el paso dentro de la transacción del esquema.</summary>
    /// <param name="connection">La conexión del esquema.</param>
    /// <param name="transaction">La transacción en la que se aplica todo el esquema que falta.</param>
    internal void Run(SqliteConnection connection, SqliteTransaction transaction) => _run(connection, transaction);

    private static void ExecuteSql(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
