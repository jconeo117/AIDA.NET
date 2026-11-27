using System.Text.Json;
using Aida.Arms.Health.Desk;
using Aida.Arms.Health.Setup;
using Aida.Engine.Actions;
using Aida.Engine.Ontology;
using Aida.Platform.Audit;
using Aida.Platform.Identity;
using Aida.Platform.Keys;
using Aida.Platform.People;
using Aida.Platform.Records;
using Aida.Platform.Roles;
using Aida.Platform.Sealing;
using Xunit;

namespace Aida.Arms.Health.Tests.Desk;

public sealed class HealthDeskAuditTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly AuditLog _audit;
    private readonly HealthDesk _desk;
    private readonly UserAccount _prof;
    private readonly long _personId;

    public HealthDeskAuditTests()
    {
        var repoRoot = FindRepoRoot();
        var time = new ManualTime();
        _audit = new AuditLog(_db.Database, time, HealthDeclarations.AuditKinds);
        var roles = RoleCatalog.Load(Path.Combine(repoRoot, "packs", "platform", "roles.yaml"), [.. HealthDeclarations.Permissions, "agenda.manage", "agenda.block"]);
        var users = new UserStore(_db.Database, _audit, roles, new PasswordHasher(10), time);
        var template = EncounterTemplate.Load(Path.Combine(repoRoot, "packs", "health", "encounter.yaml"));
        var manifest = HealthManifest.Load(Path.Combine(repoRoot, "packs", "health", "arm.yaml"));

        var keyPath = Path.Combine(_db.DirectoryPath, "machine.key");
        var machineKey = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
        _desk = new HealthDesk(_db.Database, _audit, roles, users, template, manifest.Actions, Reversibility.Reversible, machineKey, time);

        var admin = users.CreateFirstAdministrator("admin", "Admin", "Admin1234!", "127.0.0.1");
        _prof = users.Create(admin.Id, "127.0.0.1", "prof1", "Dr. Uno", "profesional", "contrasena1");

        using var conn = _db.Database.Open();
        using var tx = conn.BeginTransaction();
        var people = new PersonStore(conn, tx, new Sealer(machineKey), PersonKeys.From(machineKey));
        var p = people.Add("Paciente Uno", "3001234567");
        people.UpdateDetails(p.Id, new PersonDetails("CC", "12345678", new DateOnly(1985, 1, 15), "M"));
        tx.Commit();
        _personId = p.Id;
    }

    [Fact]
    public void List_registra_auditoria_con_person()
    {
        var list = _desk.List(_prof, "puesto-1", _personId);
        Assert.NotNull(list);

        var lines = _audit.Lines();
        Assert.Contains(lines, l => l.Contains("\"kind\": \"records_listed\"") && l.Contains(_personId.ToString()));
    }

    [Fact]
    public void Open_registra_auditoria_y_obtiene_person_name()
    {
        var created = _desk.Create(_prof, "puesto-1", _personId, null);
        var recordId = long.Parse(created.Data["record"]);

        var view = _desk.Open(_prof, "puesto-1", recordId);
        Assert.NotNull(view);
        Assert.Equal("Paciente Uno", view.PersonName);

        var lines = _audit.Lines();
        Assert.Contains(lines, l => l.Contains("\"kind\": \"record_viewed\"") && l.Contains(recordId.ToString()) && l.Contains(_personId.ToString()));
    }

    [Fact]
    public void Create_y_save_registran_auditoria_changed_y_secuencia_positiva()
    {
        var res = _desk.Create(_prof, "puesto-1", _personId, null);
        Assert.Equal(ToolStatus.Done, res.Status);
        var recordId = res.Data["record"];

        var lines = _desk.EventLines();
        Assert.NotEmpty(lines);
        using var doc = JsonDocument.Parse(lines[0]);
        var tick = doc.RootElement.GetProperty("tick").GetInt64();
        Assert.True(tick > 0);

        var auditLines = _audit.Lines();
        Assert.Contains(auditLines, l => l.Contains("health_changed") && l.Contains("crear_atencion") && l.Contains(recordId) && l.Contains(_personId.ToString()));
    }

    [Fact]
    public void View_missing_sections_calcula_segun_estado_y_obligatoriedad()
    {
        var res = _desk.Create(_prof, "puesto-1", _personId, "ref");
        var recordId = long.Parse(res.Data["record"]);

        // 1. Borrador vacío: faltan las 6 secciones requeridas, pero observaciones (opcional) no
        var v1 = _desk.Open(_prof, "puesto-1", recordId);
        Assert.NotNull(v1);
        Assert.Equal(6, v1.Missing.Count);
        Assert.DoesNotContain(v1.Missing, s => s.Id == "observaciones");

        // 2. Borrador con todas las requeridas llenas
        var allReq = new Dictionary<string, string>
        {
            ["motivo_consulta"] = "Tos",
            ["enfermedad_actual"] = "Desde ayer",
            ["antecedentes"] = "Ninguno",
            ["examen"] = "Normal",
            ["diagnostico"] = "Gripe",
            ["plan"] = "Reposo",
        };
        var sRes = _desk.Save(_prof, "puesto-1", recordId, allReq);
        Assert.Equal(ToolStatus.Done, sRes.Status);

        var v2 = _desk.Open(_prof, "puesto-1", recordId);
        Assert.NotNull(v2);
        Assert.Empty(v2.Missing);

        // 3. Firmado: Missing siempre vacío
        var signRes = _desk.Sign(_prof, "puesto-1", recordId, "contrasena1");
        Assert.Equal(ToolStatus.Done, signRes.Status);

        var v3 = _desk.Open(_prof, "puesto-1", recordId);
        Assert.NotNull(v3);
        Assert.Equal(RecordStatus.Signed, v3.Status);
        Assert.Empty(v3.Missing);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    public void Dispose() => _db.Dispose();
}
