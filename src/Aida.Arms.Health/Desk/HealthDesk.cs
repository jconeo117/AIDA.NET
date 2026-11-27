using System.Globalization;
using Aida.Arms.Health;
using Aida.Arms.Health.Actions;
using Aida.Arms.Health.Setup;
using Aida.Engine.Actions;
using Aida.Engine.Events;
using Aida.Engine.Ontology;
using Aida.Platform.Audit;
using Aida.Platform.Identity;
using Aida.Platform.Keys;
using Aida.Platform.People;
using Aida.Platform.Records;
using Aida.Platform.Roles;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;

namespace Aida.Arms.Health.Desk;

/// <summary>El mostrador de la historia clínica: lo único que usa la pantalla para leer, escribir y firmar una atención.</summary>
/// <remarks>
/// <para>Todo cambio pasa por la puerta del motor, y la firma además exige la contraseña de quien firma en ese mismo momento
/// (ADR 0039). Aquí no hay ningún camino que se salte la puerta, ni que arme una petición con <c>AuthorizedBy</c> sin antes
/// haber verificado la contraseña.</para>
/// <para>Sigue el mostrador de la agenda: un cerrojo de proceso ordena las peticiones, y cada una tiene su conexión y su
/// transacción. El documento, su hecho de la puerta y su línea <c>health_changed</c> se confirman juntos.</para>
/// <para>Leer una lista o abrir una atención también deja huella (ADR 0040): <c>records_listed</c> y <c>record_viewed</c>, con
/// la persona o el documento y nunca con el contenido. Son lecturas que escriben en la auditoría, por eso van en transacción.</para>
/// </remarks>
public sealed class HealthDesk
{
    private static readonly AgentId _owner = new(HealthDeclarations.ArmId);

    private readonly PlatformDatabase _database;
    private readonly AuditLog _audit;
    private readonly RoleCatalog _roles;
    private readonly UserStore _users;
    private readonly ActionCatalog _catalog;
    private readonly Reversibility _ceiling;
    private readonly Sealer _sealer;
    private readonly PersonKeys _personKeys;
    private readonly TimeProvider _time;
    private readonly EventLog _log = new();
    private readonly Lock _sync = new();
    private long _tick;

    /// <summary>Crea el mostrador.</summary>
    /// <param name="database">La base del servidor.</param>
    /// <param name="audit">La auditoría.</param>
    /// <param name="roles">Los roles, para los tres permisos del brazo.</param>
    /// <param name="users">Los usuarios: para verificar la contraseña de quien firma y los nombres de quien escribió.</param>
    /// <param name="template">Los apartados y los textos, de <c>encounter.yaml</c>.</param>
    /// <param name="catalog">Las acciones y su reversibilidad, de <c>arm.yaml</c>.</param>
    /// <param name="ceiling">El techo de la puerta; lo fija la composición, nunca el brazo (ADR 0035).</param>
    /// <param name="machineKey">La clave del equipo, de la que salen la que sella y la de las huellas de la persona.</param>
    /// <param name="time">El reloj.</param>
    public HealthDesk(PlatformDatabase database, AuditLog audit, RoleCatalog roles, UserStore users, EncounterTemplate template,
        ActionCatalog catalog, Reversibility ceiling, MachineKey machineKey, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(machineKey);
        _database = database;
        _audit = audit;
        _roles = roles;
        _users = users;
        Template = template;
        _catalog = catalog;
        _ceiling = ceiling;
        _time = time;
        _sealer = new Sealer(machineKey);
        _personKeys = PersonKeys.From(machineKey);
    }

    /// <summary>Los apartados y los textos de la atención.</summary>
    public EncounterTemplate Template { get; }

    /// <summary>Si un usuario puede leer las atenciones y abrir una.</summary>
    /// <param name="user">El usuario.</param>
    /// <returns><see langword="true"/> si su rol tiene <see cref="HealthDeclarations.ReadPermission"/>.</returns>
    public bool CanRead(UserAccount user) => user is not null && _roles.Allows(user.RoleId, HealthDeclarations.ReadPermission);

    /// <summary>Si un usuario puede crear una atención y guardar sus campos.</summary>
    /// <param name="user">El usuario.</param>
    /// <returns><see langword="true"/> si su rol tiene <see cref="HealthDeclarations.RecordPermission"/>.</returns>
    public bool CanRecord(UserAccount user) => user is not null && _roles.Allows(user.RoleId, HealthDeclarations.RecordPermission);

    /// <summary>Si un usuario puede firmar.</summary>
    /// <param name="user">El usuario.</param>
    /// <returns><see langword="true"/> si su rol tiene <see cref="HealthDeclarations.SignPermission"/>.</returns>
    public bool CanSign(UserAccount user) => user is not null && _roles.Allows(user.RoleId, HealthDeclarations.SignPermission);

    /// <summary>Las atenciones de una persona, para su página.</summary>
    /// <param name="user">Quien mira; el de la sesión.</param>
    /// <param name="workstation">Desde qué puesto.</param>
    /// <param name="person">La persona.</param>
    /// <returns>La lista, o <see langword="null"/> si no tiene permiso de lectura o la persona no existe. Deja <c>records_listed</c>.</returns>
    public EncounterList? List(UserAccount user, string workstation, long person)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!CanRead(user))
        {
            return null;
        }

        var names = UserNames();
        lock (_sync)
        {
            using var work = HealthWork.Begin(_database, _sealer, _personKeys);
            if (work.People.Find(person) is not { } found)
            {
                return null;
            }

            var lines = work.Records.ListFor(person, HealthDeclarations.RecordKind)
                .Select(summary => new EncounterLine(summary.Id, summary.Status, names.GetValueOrDefault(summary.Author, summary.Author),
                    summary.CreatedAt, summary.SignedAt, summary.Author == user.Actor.Id))
                .ToList();
            _audit.Record(work.Connection, work.Transaction, AuditKind.RecordsListed, user.Actor, workstation,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["person"] = person.ToString(CultureInfo.InvariantCulture) });
            work.Commit();
            return new EncounterList(person, found.Name, lines);
        }
    }

    /// <summary>Una atención abierta, para leerla o para escribirla si es un borrador propio.</summary>
    /// <param name="user">Quien mira; el de la sesión.</param>
    /// <param name="workstation">Desde qué puesto.</param>
    /// <param name="record">El documento.</param>
    /// <returns>La atención, o <see langword="null"/> si no tiene permiso, no existe, no es de la clase o es un borrador de otro.
    /// Deja <c>record_viewed</c>.</returns>
    public EncounterView? Open(UserAccount user, string workstation, long record)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!CanRead(user))
        {
            return null;
        }

        var names = UserNames();
        lock (_sync)
        {
            using var work = HealthWork.Begin(_database, _sealer, _personKeys);
            if (work.Records.Open(record) is not { } content || content.Summary.Kind != HealthDeclarations.RecordKind)
            {
                return null;
            }

            var summary = content.Summary;
            var mine = summary.Author == user.Actor.Id;
            if (summary.Status == RecordStatus.Draft && !mine)
            {
                return null;
            }

            var personName = work.People.Find(summary.Person)?.Name ?? string.Empty;
            _audit.Record(work.Connection, work.Transaction, AuditKind.RecordViewed, user.Actor, workstation,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["record"] = record.ToString(CultureInfo.InvariantCulture),
                    ["person"] = summary.Person.ToString(CultureInfo.InvariantCulture),
                });
            work.Commit();
            return View(content, names, personName, mine);
        }
    }

    /// <summary>Crea un borrador de atención para una persona.</summary>
    /// <param name="user">Quien la crea; el de la sesión.</param>
    /// <param name="workstation">Desde qué puesto.</param>
    /// <param name="person">La persona.</param>
    /// <param name="reference">El texto opaco que enlaza con la cita atendida (<c>agenda:&lt;id&gt;</c>), o <see langword="null"/>.</param>
    /// <returns>Cómo acabó. Sin <see cref="HealthDeclarations.RecordPermission"/>, <c>forbidden</c>, sin pasar por la puerta.</returns>
    public ToolResult Create(UserAccount user, string workstation, long person, string? reference)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!CanRecord(user))
        {
            return ToolResult.Rejected("forbidden");
        }

        return Run(user, workstation, "crear_atencion",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["person"] = person.ToString(CultureInfo.InvariantCulture),
                ["reference"] = reference ?? string.Empty,
            },
            new Dictionary<string, string>(StringComparer.Ordinal),
            authorizedBy: null);
    }

    /// <summary>Guarda los campos de un borrador propio.</summary>
    /// <param name="user">Quien guarda; el de la sesión.</param>
    /// <param name="workstation">Desde qué puesto.</param>
    /// <param name="record">El borrador.</param>
    /// <param name="fields">Los campos por su nombre. El texto no viaja en la puerta: solo su huella.</param>
    /// <returns>Cómo acabó. Sin <see cref="HealthDeclarations.RecordPermission"/>, <c>forbidden</c>.</returns>
    /// <remarks>Los campos van a la herramienta por su constructor y la puerta recibe solo <c>fields_digest</c>, la huella de
    /// lo que se guarda (ADR 0029): así el texto clínico no queda en los hechos de la puerta.</remarks>
    public ToolResult Save(UserAccount user, string workstation, long record, IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(fields);
        if (!CanRecord(user))
        {
            return ToolResult.Rejected("forbidden");
        }

        return Run(user, workstation, "guardar_atencion",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["record"] = record.ToString(CultureInfo.InvariantCulture),
                ["fields_digest"] = RecordFields.Digest(fields),
            },
            fields,
            authorizedBy: null);
    }

    /// <summary>Firma un borrador propio, con la contraseña de quien firma repetida en este momento.</summary>
    /// <param name="user">Quien firma; el de la sesión.</param>
    /// <param name="workstation">Desde qué puesto.</param>
    /// <param name="record">El borrador.</param>
    /// <param name="password">La contraseña de quien firma, tal como se escribió.</param>
    /// <returns>Cómo acabó. Sin <see cref="HealthDeclarations.SignPermission"/>, <c>forbidden</c>; con una contraseña que no
    /// es la suya, <c>bad_password</c>; tras demasiados intentos, <c>wait</c>. En esos casos no se arma la petición.</returns>
    /// <remarks>La verificación va antes de la petición, y la autorización sale de ella: la puerta no sabe de contraseñas, confía
    /// en quien arma la petición (ADR 0039). Verificar con la cuenta de la sesión, y no con cualquiera, impide firmar en el puesto
    /// de otro con su sesión.</remarks>
    public ToolResult Sign(UserAccount user, string workstation, long record, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);
        if (!CanSign(user))
        {
            return ToolResult.Rejected("forbidden");
        }

        UserAccount verified;
        try
        {
            verified = _users.Verify(user.UserName, password);
        }
        catch (IdentityException exception)
        {
            return ToolResult.Rejected(exception.Error == IdentityError.Wait ? "wait" : "bad_password");
        }

        if (verified.Id != user.Id)
        {
            return ToolResult.Rejected("bad_password");
        }

        return Run(user, workstation, "firmar_atencion",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["record"] = record.ToString(CultureInfo.InvariantCulture) },
            new Dictionary<string, string>(StringComparer.Ordinal),
            authorizedBy: user.Actor.Id);
    }

    /// <summary>Los hechos de la puerta, en JSON canónico; para los tests.</summary>
    internal IReadOnlyList<string> EventLines()
    {
        lock (_sync)
        {
            return [.. _log.Select(recorded => recorded.ToJson())];
        }
    }

    private ToolResult Run(UserAccount user, string workstation, string action, Dictionary<string, string> arguments,
        IReadOnlyDictionary<string, string> fields, string? authorizedBy)
    {
        var by = user.Actor.Id;
        arguments["by"] = by;
        lock (_sync)
        {
            using var work = HealthWork.Begin(_database, _sealer, _personKeys);
            var tool = new HealthTool(work.Records, work.People, Template, fields, workstation, _time);
            var gate = new ActionGate(_catalog, _ceiling, tool, _log, _owner, () => ++_tick);
            var result = gate.Submit(new ActionRequest(new ActionId(action), arguments, by, authorizedBy));
            if (result.Status == ToolStatus.Done)
            {
                var record = long.Parse(result.Data["record"], NumberStyles.Integer, CultureInfo.InvariantCulture);
                var person = work.Records.Summary(record)!.Person;
                _audit.Record(work.Connection, work.Transaction, HealthDeclarations.Changed, user.Actor, workstation,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["action"] = action,
                        ["record"] = result.Data["record"],
                        ["person"] = person.ToString(CultureInfo.InvariantCulture),
                    });
                work.Commit();
            }

            return result;
        }
    }

    private EncounterView View(RecordContent content, IReadOnlyDictionary<string, string> names, string personName, bool mine)
    {
        var summary = content.Summary;
        var fields = Template.Sections
            .Select(section => KeyValuePair.Create(section, content.Fields.GetValueOrDefault(section.Id, string.Empty)))
            .ToList();
        IReadOnlyList<EncounterSection> missing = summary.Status == RecordStatus.Draft
            ? [.. fields.Where(pair => pair.Key.Required && pair.Value.Trim().Length == 0).Select(pair => pair.Key)]
            : [];
        return new EncounterView(summary.Id, summary.Person, personName, summary.Status,
            names.GetValueOrDefault(summary.Author, summary.Author), summary.CreatedAt, summary.SignedAt, content.ContentHash,
            fields, missing, summary.Status == RecordStatus.Draft && mine);
    }

    private IReadOnlyDictionary<string, string> UserNames() =>
        _users.List().ToDictionary(user => user.Actor.Id, user => user.DisplayName, StringComparer.Ordinal);
}
