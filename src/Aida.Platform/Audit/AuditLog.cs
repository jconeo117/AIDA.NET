using System.Globalization;
using Aida.Platform.Ledger;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;

namespace Aida.Platform.Audit;

/// <summary>Qué pasó, quién lo hizo y desde qué puesto, sobre la cadena común.</summary>
/// <remarks>
/// Es <c>platform/audit.py:73-101</c> de la 0.1 sin <c>owner</c> ni <c>blob</c>, que entran con el primer brazo que
/// los lea. Cada línea dice en claro cuándo, qué clase de hecho, quién, desde dónde y sus datos escalares; el
/// personal no se sella (ADR 0029). Quien escribe un hecho junto con un cambio lo hace en la misma transacción, también
/// un brazo (ADR 0032): un cambio sin su línea, o una línea sin su cambio, no existen. Solo se escriben las clases de la
/// plataforma y las que declararon los brazos (ADR 0035).
/// </remarks>
public sealed class AuditLog
{
    private readonly PlatformDatabase _database;
    private readonly TimeProvider _time;
    private readonly HashSet<AuditKind> _known;

    /// <summary>Crea la auditoría.</summary>
    /// <param name="database">La base de datos.</param>
    /// <param name="time">El reloj; la hora va en la hora local del servidor, con su desfase.</param>
    /// <param name="armKinds">Las clases de hecho que declaran los brazos; las de la plataforma se conocen siempre.</param>
    public AuditLog(PlatformDatabase database, TimeProvider time, IEnumerable<AuditKind>? armKinds = null)
    {
        _database = database;
        _time = time;
        _known = [.. AuditKind.Platform, .. armKinds ?? []];
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

    /// <summary>Registra un hecho dentro de la transacción de quien llama, junto con el cambio que lo origina.</summary>
    /// <param name="connection">La conexión de esa transacción.</param>
    /// <param name="transaction">La transacción; la confirma quien llama.</param>
    /// <param name="kind">La clase de hecho.</param>
    /// <param name="actor">Quién lo hizo.</param>
    /// <param name="workstation">Desde qué puesto.</param>
    /// <param name="data">Los datos escalares del hecho; las claves son ASCII.</param>
    /// <returns>La huella de la línea.</returns>
    /// <exception cref="ArgumentNullException">Si no hay autor.</exception>
    /// <exception cref="ArgumentException">Si la clase de hecho no la conoce nadie.</exception>
    public string Record(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AuditKind kind,
        Actor actor,
        string workstation,
        IReadOnlyDictionary<string, string> data)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(kind);
        if (!_known.Contains(kind))
        {
            throw new ArgumentException($"clase de hecho de auditoría desconocida: «{kind.Name}»", nameof(kind));
        }

        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["at"] = _time.GetLocalNow().ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            ["kind"] = kind.Name,
            ["actor"] = actor.Id,
            ["actor_name"] = actor.Name,
            ["workstation"] = workstation,
            ["data"] = LedgerJson.Write(data),
        };
        return ChainedLog.Append(connection, transaction, fields);
    }
}
