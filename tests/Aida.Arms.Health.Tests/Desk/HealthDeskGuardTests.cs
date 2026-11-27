using Aida.Arms.Health.Desk;
using Aida.Arms.Health.Setup;
using Aida.Engine.Actions;
using Aida.Engine.Ontology;
using Aida.Platform.Audit;
using Aida.Platform.Identity;
using Aida.Platform.Keys;
using Aida.Platform.People;
using Aida.Platform.Roles;
using Aida.Platform.Sealing;
using Xunit;

namespace Aida.Arms.Health.Tests.Desk;

public sealed class HealthDeskGuardTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly AuditLog _audit;
    private readonly RoleCatalog _roles;
    private readonly UserStore _users;
    private readonly EncounterTemplate _template;
    private readonly ActionCatalog _catalog;
    private readonly MachineKey _machineKey;
    private readonly ManualTime _time = new();
    private readonly HealthDesk _desk;
    private readonly UserAccount _prof;

    public HealthDeskGuardTests()
    {
        var repoRoot = FindRepoRoot();
        _audit = new AuditLog(_db.Database, _time, HealthDeclarations.AuditKinds);
        _roles = RoleCatalog.Load(Path.Combine(repoRoot, "packs", "platform", "roles.yaml"), [.. HealthDeclarations.Permissions, "agenda.manage", "agenda.block"]);
        _users = new UserStore(_db.Database, _audit, _roles, new PasswordHasher(10), _time);
        _template = EncounterTemplate.Load(Path.Combine(repoRoot, "packs", "health", "encounter.yaml"));
        var manifest = HealthManifest.Load(Path.Combine(repoRoot, "packs", "health", "arm.yaml"));
        _catalog = manifest.Actions;

        var keyPath = Path.Combine(_db.DirectoryPath, "machine.key");
        _machineKey = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
        _desk = new HealthDesk(_db.Database, _audit, _roles, _users, _template, _catalog, Reversibility.Reversible, _machineKey, _time);

        var admin = _users.CreateFirstAdministrator("admin", "Admin", "Admin1234!", "127.0.0.1");
        _prof = _users.Create(admin.Id, "127.0.0.1", "prof1", "Dr. Uno", "profesional", "contrasena1");
    }

    [Fact]
    public void Constructor_valida_nulos()
    {
        Assert.Throws<ArgumentNullException>(() => new HealthDesk(
            null!, _audit, _roles, _users, _template, _catalog, Reversibility.Reversible, _machineKey, _time));
        Assert.Throws<ArgumentNullException>(() => new HealthDesk(
            _db.Database, _audit, _roles, _users, _template, _catalog, Reversibility.Reversible, null!, _time));
    }

    [Fact]
    public void Metodos_validan_argumentos_nulos()
    {
        var emptyFields = new Dictionary<string, string>();

        Assert.Throws<ArgumentNullException>(() => _desk.List(null!, "ws", 1));
        Assert.Throws<ArgumentNullException>(() => _desk.Open(null!, "ws", 1));
        Assert.Throws<ArgumentNullException>(() => _desk.Create(null!, "ws", 1, null));
        Assert.Throws<ArgumentNullException>(() => _desk.Save(null!, "ws", 1, emptyFields));
        Assert.Throws<ArgumentNullException>(() => _desk.Save(_prof, "ws", 1, null!));
        Assert.Throws<ArgumentNullException>(() => _desk.Sign(null!, "ws", 1, "contrasena1"));
        Assert.Throws<ArgumentNullException>(() => _desk.Sign(_prof, "ws", 1, null!));
    }

    [Fact]
    public void Permisos_con_usuario_nulo_devuelven_falso()
    {
        Assert.False(_desk.CanRead(null!));
        Assert.False(_desk.CanRecord(null!));
        Assert.False(_desk.CanSign(null!));
    }

    [Fact]
    public void HealthWork_dispose_cierra_recursos()
    {
        var sealer = new Sealer(_machineKey);
        var personKeys = PersonKeys.From(_machineKey);
        var work = HealthWork.Begin(_db.Database, sealer, personKeys);
        Assert.NotNull(work.Connection);
        Assert.NotNull(work.Transaction);
        work.Dispose();
        Assert.Equal(System.Data.ConnectionState.Closed, work.Connection.State);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    public void Dispose() => _db.Dispose();
}
