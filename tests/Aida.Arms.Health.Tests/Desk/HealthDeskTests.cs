using Aida.Arms.Health;
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

public sealed class HealthDeskTests : IDisposable
{
    private readonly TestDatabase _db;
    private readonly AuditLog _audit;
    private readonly RoleCatalog _roles;
    private readonly UserStore _users;
    private readonly EncounterTemplate _template;
    private readonly HealthDesk _desk;
    private readonly ManualTime _time;
    private readonly UserAccount _p1;
    private readonly UserAccount _p2;
    private readonly UserAccount _rec;
    private readonly long _personComplete;
    private readonly long _personIncomplete;

    public HealthDeskTests()
    {
        _db = new TestDatabase();
        _time = new ManualTime();
        _audit = new AuditLog(_db.Database, _time, HealthDeclarations.AuditKinds);

        var repoRoot = FindRepoRoot();
        _roles = RoleCatalog.Load(Path.Combine(repoRoot, "packs", "platform", "roles.yaml"), [.. HealthDeclarations.Permissions, "agenda.manage", "agenda.block"]);
        _users = new UserStore(_db.Database, _audit, _roles, new PasswordHasher(10), _time);
        _template = EncounterTemplate.Load(Path.Combine(repoRoot, "packs", "health", "encounter.yaml"));
        var manifest = HealthManifest.Load(Path.Combine(repoRoot, "packs", "health", "arm.yaml"));

        var admin = _users.CreateFirstAdministrator("admin", "Admin", "Admin1234!", "127.0.0.1");
        _p1 = _users.Create(admin.Id, "127.0.0.1", "prof1", "Dr. Uno", "profesional", "contrasena1");
        _p2 = _users.Create(admin.Id, "127.0.0.1", "prof2", "Dr. Dos", "profesional", "contrasena2");
        _rec = _users.Create(admin.Id, "127.0.0.1", "rec1", "Recepcionista", "recepcion", "contrasenarec");

        var keyPath = Path.Combine(_db.DirectoryPath, "machine.key");
        var key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
        _desk = new HealthDesk(_db.Database, _audit, _roles, _users, _template, manifest.Actions, Reversibility.Reversible, key, _time);

        using var conn = _db.Database.Open();
        using var tx = conn.BeginTransaction();
        var people = new PersonStore(conn, tx, new Sealer(key), PersonKeys.From(key));
        _personComplete = people.Add("Persona Completa", "3001112233").Id;
        people.UpdateDetails(_personComplete, new PersonDetails("CC", "12345678", new DateOnly(1990, 1, 1), "F"));
        _personIncomplete = people.Add("Persona Incompleta", "3004445566").Id;
        tx.Commit();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void P1_crea_guarda_y_firma_con_su_contrasena()
    {
        var cRes = _desk.Create(_p1, "ws1", _personComplete, null);
        Assert.Equal(ToolStatus.Done, cRes.Status);
        var recordId = long.Parse(cRes.Data["record"]);

        var sRes = _desk.Save(_p1, "ws1", recordId, new Dictionary<string, string> { ["motivo_consulta"] = "Fiebre alta" });
        Assert.Equal(ToolStatus.Done, sRes.Status);

        var signRes = _desk.Sign(_p1, "ws1", recordId, "contrasena1");
        Assert.Equal(ToolStatus.Done, signRes.Status);

        var view = _desk.Open(_p1, "ws1", recordId);
        Assert.NotNull(view);
        Assert.Equal(RecordStatus.Signed, view.Status);
        Assert.NotNull(view.ContentHash);
        Assert.Empty(view.Missing);
        Assert.False(view.CanEdit);
        Assert.Equal("Fiebre alta", view.Fields.First(f => f.Key.Id == "motivo_consulta").Value);
        Assert.Equal("No consta.", view.Fields.First(f => f.Key.Id == "enfermedad_actual").Value);
    }

    [Fact]
    public void P1_firma_con_contrasena_equivocada_da_bad_password_y_sigue_borrador()
    {
        var cRes = _desk.Create(_p1, "ws1", _personComplete, null);
        var recordId = long.Parse(cRes.Data["record"]);
        var eventsBefore = _desk.EventLines().Count;

        var res = _desk.Sign(_p1, "ws1", recordId, "pass_incorrecta");
        Assert.Equal(ToolStatus.Rejected, res.Status);
        Assert.Equal("bad_password", res.Reason);
        Assert.Equal(eventsBefore, _desk.EventLines().Count);

        var view = _desk.Open(_p1, "ws1", recordId);
        Assert.NotNull(view);
        Assert.Equal(RecordStatus.Draft, view.Status);
    }

    [Fact]
    public void Cinco_contrasenas_equivocadas_da_wait()
    {
        var recordId = long.Parse(_desk.Create(_p1, "ws1", _personComplete, null).Data["record"]);
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal("bad_password", _desk.Sign(_p1, "ws1", recordId, "contrasenamala").Reason);
        }

        var r5 = _desk.Sign(_p1, "ws1", recordId, "contrasenamala");
        Assert.Equal("wait", r5.Reason);
    }

    [Fact]
    public void P2_abre_borrador_de_p1_da_null_y_List_muestra_Mine_false()
    {
        var recordId = long.Parse(_desk.Create(_p1, "ws1", _personComplete, null).Data["record"]);
        Assert.Null(_desk.Open(_p2, "ws1", recordId));

        var list = _desk.List(_p2, "ws1", _personComplete);
        Assert.NotNull(list);
        var line = Assert.Single(list.Lines);
        Assert.False(line.Mine);
    }

    [Fact]
    public void P2_guarda_o_firma_borrador_de_p1_da_not_author()
    {
        var recordId = long.Parse(_desk.Create(_p1, "ws1", _personComplete, null).Data["record"]);
        var sRes = _desk.Save(_p2, "ws1", recordId, new Dictionary<string, string> { ["motivo_consulta"] = "X" });
        Assert.Equal("not_author", sRes.Reason);

        var fRes = _desk.Sign(_p2, "ws1", recordId, "contrasena2");
        Assert.Equal("not_author", fRes.Reason);
    }

    [Fact]
    public void Recepcionista_hace_List_Open_Create_Sign_da_null_o_forbidden()
    {
        var recordId = long.Parse(_desk.Create(_p1, "ws1", _personComplete, null).Data["record"]);
        Assert.Null(_desk.List(_rec, "ws1", _personComplete));
        Assert.Null(_desk.Open(_rec, "ws1", recordId));
        Assert.Equal("forbidden", _desk.Create(_rec, "ws1", _personComplete, null).Reason);
        Assert.Equal("forbidden", _desk.Save(_rec, "ws1", recordId, new Dictionary<string, string>()).Reason);
        Assert.Equal("forbidden", _desk.Sign(_rec, "ws1", recordId, "contrasenarec").Reason);
    }

    [Fact]
    public void Create_con_persona_sin_documento_da_incomplete_person()
    {
        var res = _desk.Create(_p1, "ws1", _personIncomplete, null);
        Assert.Equal(ToolStatus.Rejected, res.Status);
        Assert.Equal("incomplete_person", res.Reason);
    }

    [Fact]
    public void Save_no_deja_texto_en_los_hechos_de_la_puerta()
    {
        var recordId = long.Parse(_desk.Create(_p1, "ws1", _personComplete, null).Data["record"]);
        const string secreto = "DiagnosticoSuperPrivado999";
        _desk.Save(_p1, "ws1", recordId, new Dictionary<string, string> { ["motivo_consulta"] = secreto });

        var events = _desk.EventLines();
        Assert.Contains(events, e => e.Contains("fields_digest"));
        Assert.DoesNotContain(events, e => e.Contains(secreto));
    }

    [Fact]
    public void List_y_Open_dejan_records_listed_y_record_viewed_en_auditoria_sin_contenido()
    {
        var recordId = long.Parse(_desk.Create(_p1, "ws1", _personComplete, null).Data["record"]);
        const string secreto = "TextoOcultoAuditoria";
        _desk.Save(_p1, "ws1", recordId, new Dictionary<string, string> { ["motivo_consulta"] = secreto });

        _desk.List(_p1, "ws1", _personComplete);
        _desk.Open(_p1, "ws1", recordId);

        var auditLines = _audit.Lines();
        Assert.Contains(auditLines, l => l.Contains("\"kind\": \"records_listed\"") && l.Contains(_personComplete.ToString()));
        Assert.Contains(auditLines, l => l.Contains("\"kind\": \"record_viewed\"") && l.Contains(recordId.ToString()));
        Assert.DoesNotContain(auditLines, l => l.Contains(secreto));
    }

    [Fact]
    public void Firmar_emite_requested_authorized_y_completed()
    {
        var recordId = long.Parse(_desk.Create(_p1, "ws1", _personComplete, null).Data["record"]);
        _desk.Sign(_p1, "ws1", recordId, "contrasena1");

        var events = _desk.EventLines().ToList();
        var signReqIndex = events.FindLastIndex(e => e.Contains("action_requested") && e.Contains("firmar_atencion"));
        var authIndex = events.FindLastIndex(e => e.Contains("action_authorized") && e.Contains(_p1.Actor.Id));
        var compIndex = events.FindLastIndex(e => e.Contains("action_completed") && e.Contains("firmar_atencion"));

        Assert.True(signReqIndex >= 0 && authIndex > signReqIndex && compIndex > authIndex);
    }

    [Fact]
    public void CanRead_CanRecord_CanSign_verifican_permisos()
    {
        Assert.True(_desk.CanRead(_p1));
        Assert.True(_desk.CanRecord(_p1));
        Assert.True(_desk.CanSign(_p1));
        Assert.False(_desk.CanRead(_rec));
        Assert.False(_desk.CanRecord(_rec));
        Assert.False(_desk.CanSign(_rec));
    }
}
