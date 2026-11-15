using System.Globalization;
using Aida.Platform.Ledger;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;

namespace Aida.Platform.Audit;

/// <summary>Qué pasó, quién lo hizo y desde qué puesto, sobre la cadena común.</summary>
/// <remarks>
/// Es <c>platform/audit.py:73-101</c> de la 0.1 sin <c>owner</c> ni <c>blob</c>, que entran con el primer brazo que
/// los use. Cada línea dice en claro cuándo, qué clase de hecho, quién, desde dónde y sus datos escalares; el
/// personal no se sella (ADR 0029). Quien escribe un hecho junto con un cambio lo hace en la misma transacción: un
/// usuario creado sin su línea, o una línea sin su usuario, no existen.
/// </remarks>
public sealed class AuditLog
{
    private readonly PlatformDatabase _database;
    private readonly TimeProvider _time;

    /// <summary>Crea la auditoría.</summary>
    /// <param name="database">La base de datos.</param>
    /// <param name="time">El reloj; la hora va en la hora local del servidor, con su desfase.</param>
    public AuditLog(PlatformDatabase database, TimeProvider time)
    {
        _database = database;
        _time = time;
    }

    /// <summary>Registra un hecho en su propia transacción.</summary>
    /// <param name="kind">La clase de hecho.</param>
    /// <param name="actor">Quién lo hizo.</param>
    /// <param name="workstation">Desde qué puesto: la dirección de red del navegador.</param>
    /// <param name="data">Los datos escalares del hecho; las claves son ASCII.</param>
    /// <returns>La huella de la línea.</returns>
    public string Record(AuditKind kind, Actor actor, string workstation, IReadOnlyDictionary<string, string> data)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        var digest = Record(connection, transaction, kind, actor, workstation, data);
        transaction.Commit();
        return digest;
    }

    /// <summary>Las líneas, en orden, tal como se guardaron.</summary>
    /// <returns>Una línea JSON por hecho.</returns>
    public IReadOnlyList<string> Lines()
    {
        using var connection = _database.Open();
        return ChainedLog.Lines(connection);
    }

    /// <summary>Los problemas de la cadena; vacío si está íntegra.</summary>
    /// <returns>Un texto por problema, con su número de línea.</returns>
    public IReadOnlyList<string> Verify()
    {
        using var connection = _database.Open();
        return ChainedLog.Verify(connection);
    }

    internal string Record(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AuditKind kind,
        Actor actor,
        string workstation,
        IReadOnlyDictionary<string, string> data)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["at"] = _time.GetLocalNow().ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            ["kind"] = WireName(kind),
            ["actor"] = actor.Id,
            ["actor_name"] = actor.Name,
            ["workstation"] = workstation,
            ["data"] = LedgerJson.Write(data),
        };
        return ChainedLog.Append(connection, transaction, fields);
    }

    private static string WireName(AuditKind kind) => kind switch
    {
        AuditKind.SessionOpened => "session_opened",
        AuditKind.SessionClosed => "session_closed",
        AuditKind.Locked => "locked",
        AuditKind.Unlocked => "unlocked",
        AuditKind.UserCreated => "user_created",
        AuditKind.UserDisabled => "user_disabled",
        AuditKind.PasswordReset => "password_reset",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "clase de hecho de auditoría desconocida"),
    };
}
