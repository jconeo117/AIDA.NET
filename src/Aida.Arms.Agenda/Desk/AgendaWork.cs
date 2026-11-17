using Aida.Arms.Agenda.Storage;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;

namespace Aida.Arms.Agenda.Desk;

/// <summary>Una petición a la agenda: su conexión, su transacción y el almacén dentro de ella.</summary>
/// <remarks>Lo que cambia junto se confirma junto (ADR 0028, ADR 0032): la cita, la persona y el hecho de auditoría van en
/// la misma transacción. Si no se confirma, al cerrarse se deshace. La transacción es inmediata: toma el permiso de
/// escritura al empezar, y dos puestos que agendan el mismo hueco no ven los dos el hueco libre.</remarks>
internal sealed class AgendaWork : IDisposable
{
    private AgendaWork(SqliteConnection connection, SqliteTransaction transaction, AgendaStore store)
    {
        Connection = connection;
        Transaction = transaction;
        Store = store;
    }

    internal SqliteConnection Connection { get; }

    internal SqliteTransaction Transaction { get; }

    internal AgendaStore Store { get; }

    internal static AgendaWork Begin(PlatformDatabase database, Sealer sealer, byte[] phoneKey)
    {
        var connection = database.Open();
        var transaction = connection.BeginTransaction(deferred: false);
        return new AgendaWork(connection, transaction, new AgendaStore(connection, transaction, sealer, phoneKey));
    }

    internal void Commit() => Transaction.Commit();

    /// <inheritdoc/>
    public void Dispose()
    {
        Transaction.Dispose();
        Connection.Dispose();
    }
}
