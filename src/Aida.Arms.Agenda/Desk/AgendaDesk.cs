using System.Collections.Immutable;
using System.Globalization;
using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;
using Aida.Engine.Events;
using Aida.Engine.Ontology;
using Aida.Platform.Audit;
using Aida.Platform.Identity;
using Aida.Platform.Keys;
using Aida.Platform.People;
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
/// <item>cambiar una cita pide <see cref="AgendaDeclarations.ManagePermission"/>, y sin él no se llega a la puerta;</item>
/// <item>bloquear y desbloquear piden <see cref="AgendaDeclarations.BlockPermission"/>, que el profesional no tiene;</item>
/// <item>la puerta compara con el techo que fija la composición (ADR 0035);</item>
/// <item>la cita o el bloqueo y su hecho <c>agenda_changed</c> se confirman en la misma transacción (ADR 0032).</item>
/// </list>
/// <para>El registro de la puerta vive en memoria, como en la 0.1 (<c>app.py:104</c>); lo que dura es la auditoría. Un
/// cerrojo de proceso ordena las peticiones de todos los puestos, como el <c>lock</c> de la 0.1.</para>
/// </remarks>
public sealed class AgendaDesk
{
    private static readonly AgentId _owner = new(AgendaDeclarations.ArmId);
    private static readonly ImmutableArray<string> _subjects = ["appointment", "block", "client"];

    private readonly PlatformDatabase _database;
    private readonly AuditLog _audit;
    private readonly RoleCatalog _roles;
    private readonly UserStore _users;
    private readonly ActionCatalog _catalog;
    private readonly Reversibility _ceiling;
    private readonly TimeProvider _time;
    private readonly Sealer _sealer;
    private readonly PersonKeys _personKeys;
    private readonly EventLog _log = new();
    private readonly Lock _sync = new();
    private long _tick;

    /// <summary>Crea el mostrador y deja al día las tablas de la agenda.</summary>
    /// <param name="database">La base del servidor.</param>
    /// <param name="audit">La auditoría.</param>
    /// <param name="roles">Los roles, para el permiso de cambiar y el de bloquear.</param>
    /// <param name="users">Los usuarios, para los nombres de quien agendó.</param>
    /// <param name="setup">La configuración de la clínica.</param>
    /// <param name="people">Los tipos de documento y los sexos, de <c>people.yaml</c>.</param>
    /// <param name="catalog">Las acciones y su reversibilidad, de <c>arm.yaml</c>.</param>
    /// <param name="ceiling">El techo de la puerta; lo fija la composición.</param>
    /// <param name="machineKey">La clave del equipo, de la que salen la que sella y la de la huella del teléfono.</param>
    /// <param name="time">El reloj.</param>
    /// <exception cref="InvalidOperationException">Si la base tiene un esquema de la agenda más nuevo que este código.</exception>
    public AgendaDesk(PlatformDatabase database, AuditLog audit, RoleCatalog roles, UserStore users, AgendaSetup setup, PersonCatalog people, ActionCatalog catalog, Reversibility ceiling, MachineKey machineKey, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(machineKey);
        _database = database;
        _audit = audit;
        _roles = roles;
        _users = users;
        Setup = setup;
        People = people;
        _catalog = catalog;
        _ceiling = ceiling;
        _time = time;
        _sealer = new Sealer(machineKey);
        _personKeys = PersonKeys.From(machineKey);
        database.ApplyArmSchema(AgendaDeclarations.ArmId, AgendaSchema.Steps(_sealer, _personKeys));
    }

    /// <summary>La configuración de la clínica.</summary>
    public AgendaSetup Setup { get; }

    /// <summary>Los tipos de documento y los sexos que acepta una persona.</summary>
    /// <remarks>Lo lee la ficha para pintar los selectores, y la puerta para validar lo que llega. Viene del pack, no del código
    /// (ADR 0038).</remarks>
    public PersonCatalog People { get; }

    /// <summary>Si un usuario puede cambiar la agenda.</summary>
    /// <param name="user">El usuario.</param>
    /// <returns><see langword="true"/> si su rol tiene el permiso.</returns>
    public bool CanManage(UserAccount user) => user is not null && _roles.Allows(user.RoleId, AgendaDeclarations.ManagePermission);

    /// <summary>Si un usuario puede bloquear y desbloquear.</summary>
    /// <param name="user">El usuario.</param>
    /// <returns><see langword="true"/> si su rol tiene <see cref="AgendaDeclarations.BlockPermission"/>.</returns>
    public bool CanBlock(UserAccount user) => user is not null && _roles.Allows(user.RoleId, AgendaDeclarations.BlockPermission);

    /// <summary>El día, para pintarlo.</summary>
    /// <param name="day">El día.</param>
    /// <returns>Las citas, los bloqueos, los huecos libres de cada servicio, la semana y la configuración.</returns>
    public DayView Day(DateOnly day)
    {
        var names = UserNames();
        lock (_sync)
        {
            using var work = AgendaWork.Begin(_database, _sealer, _personKeys);
            var now = _time.GetLocalNow();
            var start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), now.Offset);
            var appointments = work.Store.Between(start, start.AddDays(1))
                .Select(appointment => AppointmentViews.Of(work.People, Setup, names, appointment))
                .ToList();
            var blocks = work.Store.BlocksBetween(start, start.AddDays(1))
                .Select(block => new BlockView(block, names.GetValueOrDefault(block.CreatedBy, block.CreatedBy)))
                .ToList();
            var free = Setup.Services.ToDictionary(
                service => service.Id,
                service => FreeSlots.For(work.Store, Setup, day, service.Id, now),
                StringComparer.Ordinal);
            var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
            var week = WeekSummary.For(work.Store, Setup, monday, now.Offset);
            return new DayView(day, now, Setup, appointments, blocks, free, week);
        }
    }

    /// <summary>Pide un cambio: pasa por la puerta y, si se hace, queda en la auditoría.</summary>
    /// <param name="user">Quien lo pide, el de la sesión.</param>
    /// <param name="workstation">Desde qué puesto.</param>
    /// <param name="action">La acción: <c>agendar</c>, <c>editar</c>, <c>reprogramar</c>, <c>cancelar</c>, <c>atendida</c>,
    /// <c>no_asistio</c>, <c>bloquear</c>, <c>desbloquear</c> o <c>editar_contacto</c>.</param>
    /// <param name="arguments">Sus argumentos. Un <c>by</c> que venga en ellos se sobrescribe con quien pide.</param>
    /// <returns>Cómo acabó. Sin el permiso de la acción, un rechazo <c>forbidden</c> que no llega a la puerta ni deja ningún evento.</returns>
    public ToolResult Submit(UserAccount user, string workstation, string action, IReadOnlyDictionary<string, string> arguments)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(arguments);
        if (user is null || !_roles.Allows(user.RoleId, PermissionFor(action)))
        {
            return ToolResult.Rejected("forbidden");
        }

        var by = user.Actor.Id;
        var withAuthor = new Dictionary<string, string>(arguments, StringComparer.Ordinal) { ["by"] = by };
        lock (_sync)
        {
            using var work = AgendaWork.Begin(_database, _sealer, _personKeys);
            var gate = new ActionGate(_catalog, _ceiling, new AgendaTool(work.Store, work.People, Setup, People, _time), _log, _owner, () => ++_tick);
            var result = gate.Submit(new ActionRequest(new ActionId(action), withAuthor, by));
            if (result.Status == ToolStatus.Done)
            {
                var data = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["action"] = action,
                    ["start"] = withAuthor.GetValueOrDefault("start", string.Empty),
                };
                foreach (var subject in _subjects)
                {
                    if (result.Data.TryGetValue(subject, out var id))
                    {
                        data[subject] = id;
                        break;
                    }
                }

                _audit.Record(work.Connection, work.Transaction, AgendaDeclarations.Changed, user.Actor, workstation, data);
                work.Commit();
            }

            return result;
        }
    }

    /// <summary>Las citas que impiden un bloqueo, para explicar el rechazo.</summary>
    /// <param name="result">Lo que devolvió <see cref="Submit"/>.</param>
    /// <returns>Las citas de <c>appointments</c>, en su orden, si el rechazo es <c>has_appointments</c>; si no, ninguna.</returns>
    /// <remarks>Es la lista <c>conflicts</c> de la 0.1 (<c>app.py:581-592</c>). Se lee aparte, y no dentro de
    /// <see cref="Submit"/>, para que la puerta siga devolviendo solo su resultado. Una cita que ya no existe se omite.</remarks>
    public IReadOnlyList<AppointmentView> Conflicts(ToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Reason != "has_appointments" || !result.Data.TryGetValue("appointments", out var ids))
        {
            return [];
        }

        var names = UserNames();
        lock (_sync)
        {
            using var work = AgendaWork.Begin(_database, _sealer, _personKeys);
            return [.. ids.Split(',')
                .Select(text => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? work.Store.Get(id) : null)
                .OfType<Appointment>()
                .Select(appointment => AppointmentViews.Of(work.People, Setup, names, appointment))];
        }
    }

    /// <summary>Las personas que ya tienen el teléfono de una cita nueva, tras un rechazo <c>phone_known</c>.</summary>
    /// <param name="result">Lo que devolvió <see cref="Submit"/>.</param>
    /// <returns>Las personas, en el orden del rechazo; ninguna si el rechazo es otro.</returns>
    /// <remarks>Para que el operador diga si es una de ellas o una persona nueva (N015-fix). Solo lee.</remarks>
    public IReadOnlyList<Person> SamePhone(ToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Reason != "phone_known" || !result.Data.TryGetValue("people", out var ids))
        {
            return [];
        }

        lock (_sync)
        {
            using var work = AgendaWork.Begin(_database, _sealer, _personKeys);
            return [.. ids.Split(',')
                .Select(text => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? work.People.Find(id) : null)
                .OfType<Person>()];
        }
    }

    /// <summary>Busca contactos por nombre o por teléfono.</summary>
    /// <param name="query">Lo tecleado.</param>
    /// <returns>Hasta 20 contactos, por nombre sin tildes y luego por id; ninguno si la búsqueda está vacía.</returns>
    /// <remarks>Cualquier sesión busca, como ve el día (debate de N010, D3). No deja auditoría ni evento: no cambia nada.</remarks>
    public IReadOnlyList<Client> Search(string query)
    {
        lock (_sync)
        {
            using var work = AgendaWork.Begin(_database, _sealer, _personKeys);
            return ClientSearch.Find(work.People, query);
        }
    }

    /// <summary>La ficha de un contacto.</summary>
    /// <param name="client">El id del contacto.</param>
    /// <returns>La ficha, o <see langword="null"/> si el contacto no existe.</returns>
    /// <remarks>Cualquier sesión la ve (D3). Las cifras las cuenta <see cref="ClientCard"/>.</remarks>
    public ClientCard? Card(long client)
    {
        var names = UserNames();
        lock (_sync)
        {
            using var work = AgendaWork.Begin(_database, _sealer, _personKeys);
            if (work.People.Find(client) is not { } found)
            {
                return null;
            }

            var views = work.Store.AppointmentsFor(found.Id)
                .Select(appointment => AppointmentViews.Of(work.People, Setup, names, appointment))
                .ToList();
            return ClientCard.Build(found, views, _time.GetLocalNow());
        }
    }

    /// <summary>Una cita por su id, sea del día que sea.</summary>
    /// <param name="appointment">El id.</param>
    /// <returns>La cita, como la pinta la pantalla, o <see langword="null"/> si no existe.</returns>
    /// <remarks>Para abrir «mover» o «cancelar» desde otro día que el de la cita (D16). Cualquier sesión la ve, como el día.
    /// No deja auditoría ni evento: no cambia nada.</remarks>
    public AppointmentView? Appointment(long appointment)
    {
        var names = UserNames();
        lock (_sync)
        {
            using var work = AgendaWork.Begin(_database, _sealer, _personKeys);
            return work.Store.Get(appointment) is { } found
                ? AppointmentViews.Of(work.People, Setup, names, found)
                : null;
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

    /// <summary>El permiso que pide cada acción: los bloqueos, el suyo; las demás, el de cambiar la agenda.</summary>
    /// <remarks>Una acción que no existe pide el de cambiar, y la puerta la rechaza después con <c>unknown_action</c>.</remarks>
    private static string PermissionFor(string action) =>
        action is "bloquear" or "desbloquear" ? AgendaDeclarations.BlockPermission : AgendaDeclarations.ManagePermission;

    private IReadOnlyDictionary<string, string> UserNames() =>
        _users.List().ToDictionary(user => user.Actor.Id, user => user.DisplayName, StringComparer.Ordinal);
}
