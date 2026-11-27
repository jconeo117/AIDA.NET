using Aida.Platform.Keys;
using Aida.Platform.People;
using Aida.Platform.Records;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;

namespace Aida.Arms.Health.Desk;

/// <summary>Una petición a la historia clínica: su conexión, su transacción, el almacén de los documentos y el de las personas.</summary>
/// <remarks>Lo que cambia junto se confirma junto (ADR 0028, ADR 0032): el documento, su hecho de auditoría y la lectura de la
/// persona van en la misma transacción. Si no se confirma, al cerrarse se deshace. La transacción es inmediata: toma el permiso
/// de escritura al empezar, y dos puestos que firman a la vez no ven los dos el borrador sin firmar.</remarks>
internal sealed class HealthWork : IDisposable
{
    private HealthWork(SqliteConnection connection, SqliteTransaction transaction, RecordStore records, PersonStore people)
    {
        Connection = connection;
        Transaction = transaction;
        Records = records;
        People = people;
    }

    internal SqliteConnection Connection { get; }

    internal SqliteTransaction Transaction { get; }

    internal RecordStore Records { get; }

    internal PersonStore People { get; }

    internal static HealthWork Begin(PlatformDatabase database, Sealer sealer, PersonKeys personKeys)
    {
        var connection = database.Open();
        var transaction = connection.BeginTransaction(deferred: false);
        return new HealthWork(
            connection,
            transaction,
            new RecordStore(connection, transaction, sealer),
            new PersonStore(connection, transaction, sealer, personKeys));
    }

    internal void Commit() => Transaction.Commit();

    /// <inheritdoc/>
    public void Dispose()
    {
        Transaction.Dispose();
        Connection.Dispose();
    }
}
