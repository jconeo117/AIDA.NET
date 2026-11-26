using Aida.Arms.Agenda.Storage;
using Aida.Platform.People;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;

namespace Aida.Arms.Agenda.Desk;

/// <summary>Una petición a la agenda: su conexión, su transacción, el almacén de la agenda y el de las personas dentro de ella.</summary>
/// <remarks>Lo que cambia junto se confirma junto (ADR 0028, ADR 0032): la cita, la persona y el hecho de auditoría van en
/// la misma transacción. Si no se confirma, al cerrarse se deshace. La transacción es inmediata: toma el permiso de
/// escritura al empezar, y dos puestos que agendan el mismo hueco no ven los dos el hueco libre.</remarks>
internal sealed class AgendaWork : IDisposable
{
    private AgendaWork(SqliteConnection connection, SqliteTransaction transaction, AgendaStore store, PersonStore people)
    {
        Connection = connection;
        Transaction = transaction;
        Store = store;
        People = people;
    }

    internal SqliteConnection Connection { get; }

    internal SqliteTransaction Transaction { get; }

    internal AgendaStore Store { get; }

    internal PersonStore People { get; }

    internal static AgendaWork Begin(PlatformDatabase database, Sealer sealer, PersonKeys personKeys)
    {
        var connection = database.Open();
        var transaction = connection.BeginTransaction(deferred: false);
        return new AgendaWork(
            connection,
            transaction,
            new AgendaStore(connection, transaction, sealer),
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
