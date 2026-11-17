using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;
using Aida.Engine.Events;
using Aida.Engine.Ontology;
using Aida.Platform.Audit;
using Aida.Platform.Identity;
using Aida.Platform.Keys;
using Aida.Platform.Roles;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;

namespace Aida.Arms.Agenda.Desk;

/// <summary>El mostrador de la agenda: lo único que usa la pantalla para ver el día y pedir un cambio.</summary>
/// <remarks>
/// <para>Es el lado de servidor de <c>AgendaArm</c> en la 0.1 (<c>app.py:75-118, 268-338, 544-603</c>). Todo cambio pasa
/// por la puerta del motor; aquí no hay ningún camino por botón que se la salte.</para>
/// <para>Cambia respecto a la 0.1:</para>
/// <list type="bullet">
/// <item>cambiar la agenda pide <see cref="AgendaDeclarations.ManagePermission"/>, y sin él no se llega a la puerta;</item>
/// <item>la puerta compara con el techo que fija la composición (ADR 0035);</item>
/// <item>la cita y su hecho <c>agenda_changed</c> se confirman en la misma transacción (ADR 0032).</item>
/// </list>
/// <para>El registro de la puerta vive en memoria, como en la 0.1 (<c>app.py:104</c>); lo que dura es la auditoría. Un
/// cerrojo de proceso ordena las peticiones de todos los puestos, como el <c>lock</c> de la 0.1.</para>
/// </remarks>
public sealed class AgendaDesk
{
    private static readonly AgentId _owner = new(AgendaDeclarations.ArmId);

    private readonly PlatformDatabase _database;
    private readonly AuditLog _audit;
    private readonly RoleCatalog _roles;
    private readonly UserStore _users;
    private readonly ActionCatalog _catalog;
    private readonly Reversibility _ceiling;
    private readonly TimeProvider _time;
    private readonly Sealer _sealer;
    private readonly byte[] _phoneKey;
    private readonly EventLog _log = new();
    private readonly Lock _sync = new();
    private long _tick;

    /// <summary>Crea el mostrador y deja al día las tablas de la agenda.</summary>
    /// <param name="database">La base del servidor.</param>
    /// <param name="audit">La auditoría.</param>
    /// <param name="roles">Los roles, para el permiso de cambiar.</param>
    /// <param name="users">Los usuarios, para los nombres de quien agendó.</param>
    /// <param name="setup">La configuración de la clínica.</param>
    /// <param name="catalog">Las acciones y su reversibilidad, de <c>arm.yaml</c>.</param>
    /// <param name="ceiling">El techo de la puerta; lo fija la composición.</param>
    /// <param name="machineKey">La clave del equipo, de la que salen la que sella y la de la huella del teléfono.</param>
    /// <param name="time">El reloj.</param>
    /// <exception cref="InvalidOperationException">Si la base tiene un esquema de la agenda más nuevo que este código.</exception>
    public AgendaDesk(PlatformDatabase database, AuditLog audit, RoleCatalog roles, UserStore users, AgendaSetup setup, ActionCatalog catalog, Reversibility ceiling, MachineKey machineKey, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(machineKey);
        _database = database;
        _audit = audit;
        _roles = roles;
        _users = users;
        Setup = setup;
        _catalog = catalog;
        _ceiling = ceiling;
        _time = time;
        _sealer = new Sealer(machineKey);
        _phoneKey = machineKey.Derive(AgendaStore.PhoneKeyPurpose);
        database.ApplyArmSchema(AgendaDeclarations.ArmId, AgendaSchema.Steps);
    }

    /// <summary>La configuración de la clínica.</summary>
    public AgendaSetup Setup { get; }

    /// <summary>Si un usuario puede cambiar la agenda.</summary>
    /// <param name="user">El usuario.</param>
    /// <returns><see langword="true"/> si su rol tiene el permiso.</returns>
    public bool CanManage(UserAccount user) => user is not null && _roles.Allows(user.RoleId, AgendaDeclarations.ManagePermission);

    /// <summary>El día, para pintarlo.</summary>
    /// <param name="day">El día.</param>
    /// <returns>Las citas, los huecos libres de cada servicio y la configuración.</returns>
    public DayView Day(DateOnly day)
    {
        var names = _users.List().ToDictionary(user => user.Actor.Id, user => user.DisplayName, StringComparer.Ordinal);
        lock (_sync)
        {
            using var work = AgendaWork.Begin(_database, _sealer, _phoneKey);
            var now = _time.GetLocalNow();
            var start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), now.Offset);
            var appointments = work.Store.Between(start, start.AddDays(1)).Select(appointment =>
            {
                var client = work.Store.GetClient(appointment.ClientId);
                return new AppointmentView(
                    appointment,
                    client.Name,
                    client.Phone,
                    Setup.Service(appointment.Service)?.Label ?? appointment.Service,
                    names.GetValueOrDefault(appointment.CreatedBy, appointment.CreatedBy),
                    names.GetValueOrDefault(appointment.UpdatedBy, appointment.UpdatedBy));
            }).ToList();
            var free = Setup.Services.ToDictionary(
                service => service.Id,
                service => FreeSlots.For(work.Store, Setup, day, service.Id, now),
                StringComparer.Ordinal);
            return new DayView(day, now, Setup, appointments, free);
        }
    }

    /// <summary>Pide un cambio: pasa por la puerta y, si se hace, queda en la auditoría.</summary>
    /// <param name="user">Quien lo pide, el de la sesión.</param>
    /// <param name="workstation">Desde qué puesto.</param>
    /// <param name="action">La acción (<c>agendar</c>, <c>reprogramar</c>, <c>cancelar</c>, <c>atendida</c>, <c>no_asistio</c>).</param>
    /// <param name="arguments">Sus argumentos. Un <c>by</c> que venga en ellos se sobrescribe con quien pide.</param>
    /// <returns>Cómo acabó. Sin el permiso, un rechazo <c>forbidden</c> que no llega a la puerta ni deja ningún evento.</returns>
    public ToolResult Submit(UserAccount user, string workstation, string action, IReadOnlyDictionary<string, string> arguments)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(arguments);
        if (!CanManage(user))
        {
            return ToolResult.Rejected("forbidden");
        }

        var by = user.Actor.Id;
        var withAuthor = new Dictionary<string, string>(arguments, StringComparer.Ordinal) { ["by"] = by };
        lock (_sync)
        {
            using var work = AgendaWork.Begin(_database, _sealer, _phoneKey);
            var gate = new ActionGate(_catalog, _ceiling, new AgendaTool(work.Store, Setup, _time), _log, _owner, () => ++_tick);
            var result = gate.Submit(new ActionRequest(new ActionId(action), withAuthor, by));
            if (result.Status == ToolStatus.Done)
            {
                var data = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["action"] = action,
                    ["appointment"] = result.Data.GetValueOrDefault("appointment", string.Empty),
                    ["start"] = withAuthor.GetValueOrDefault("start", string.Empty),
                };
                _audit.Record(work.Connection, work.Transaction, AgendaDeclarations.Changed, user.Actor, workstation, data);
                work.Commit();
            }

            return result;
        }
    }

    /// <summary>Los eventos de la puerta, en JSON canónico; para los tests.</summary>
    internal IReadOnlyList<string> EventLines()
    {
        lock (_sync)
        {
            return [.. _log.Select(recorded => recorded.ToJson())];
        }
    }
}
