using Aida.Arms.Health.Actions;
using Aida.Arms.Health.Setup;
using Aida.Engine.Actions;
using Aida.Engine.Ontology;
using Aida.Platform.Keys;
using Aida.Platform.People;
using Aida.Platform.Records;
using Aida.Platform.Sealing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Health.Tests.Actions;

public sealed class HealthToolTests : IDisposable
{
    private readonly TestDatabase _db;
    private readonly SqliteConnection _conn;
    private readonly SqliteTransaction _tx;
    private readonly Sealer _sealer;
    private readonly PersonKeys _keys;
    private readonly EncounterTemplate _template;
    private readonly ManualTime _time;
    private readonly PersonStore _people;
    private readonly RecordStore _records;

    public HealthToolTests()
    {
        _db = new TestDatabase();
        _conn = _db.Database.Open();
        _tx = _conn.BeginTransaction();
        var keyPath = Path.Combine(_db.DirectoryPath, "machine.key");
        var key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
        _sealer = new Sealer(key);
        _keys = PersonKeys.From(key);
        _time = new ManualTime();

        var repoRoot = FindRepoRoot();
        _template = EncounterTemplate.Load(Path.Combine(repoRoot, "packs", "health", "encounter.yaml"));
        _people = new PersonStore(_conn, _tx, _sealer, _keys);
        _records = new RecordStore(_conn, _tx, _sealer);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    public void Dispose()
    {
        _tx.Dispose();
        _conn.Dispose();
        _db.Dispose();
    }

    private long CreateCompletePerson()
    {
        var p = _people.Add("Ana Lopez", "3001234567");
        _people.UpdateDetails(p.Id, new PersonDetails("CC", "12345678", new DateOnly(1990, 5, 20), "F"));
        return p.Id;
    }

    private HealthTool CreateTool(IReadOnlyDictionary<string, string>? fields = null, string workstation = "test-ws") =>
        new(_records, _people, _template, fields ?? new Dictionary<string, string>(), workstation, _time);

    [Fact]
    public void Accion_desconocida_rechaza()
    {
        var tool = CreateTool();
        var res = tool.Execute(new ActionId("inexistente"), new Dictionary<string, string>());
        Assert.Equal(ToolStatus.Rejected, res.Status);
        Assert.Equal("unknown_action", res.Reason);
    }

    [Fact]
    public void Crear_atencion_rechaza_por_orden()
    {
        var tool = CreateTool();

        // 1. unknown_person: persona inexistente o id inválido
        var r1 = tool.Execute(new ActionId("crear_atencion"), new Dictionary<string, string> { ["person"] = "9999", ["by"] = "prof1" });
        Assert.Equal(ToolStatus.Rejected, r1.Status);
        Assert.Equal("unknown_person", r1.Reason);

        // 2. incomplete_person: persona sin detalles completos
        var incompletePerson = _people.Add("Sin Datos", "3009998877").Id;
        var r2 = tool.Execute(new ActionId("crear_atencion"), new Dictionary<string, string> { ["person"] = incompletePerson.ToString(), ["by"] = "prof1" });
        Assert.Equal(ToolStatus.Rejected, r2.Status);
        Assert.Equal("incomplete_person", r2.Reason);

        // 3. Éxito con persona completa
        var completePerson = CreateCompletePerson();
        var r3 = tool.Execute(new ActionId("crear_atencion"), new Dictionary<string, string>
        {
            ["person"] = completePerson.ToString(),
            ["by"] = "prof1",
            ["reference"] = "agenda:42",
        });
        Assert.Equal(ToolStatus.Done, r3.Status);
        var recordId = long.Parse(r3.Data["record"]);
        var created = _records.Open(recordId);
        Assert.NotNull(created);
        Assert.Equal("agenda:42", created.Summary.Reference);

        // 4. Éxito con reference en blanco guarda null
        var r4 = tool.Execute(new ActionId("crear_atencion"), new Dictionary<string, string>
        {
            ["person"] = completePerson.ToString(),
            ["by"] = "prof1",
            ["reference"] = "   ",
        });
        Assert.Equal(ToolStatus.Done, r4.Status);
        var created4 = _records.Open(long.Parse(r4.Data["record"]));
        Assert.NotNull(created4);
        Assert.Null(created4.Summary.Reference);
    }

    [Fact]
    public void Guardar_atencion_rechaza_por_orden()
    {
        var person = CreateCompletePerson();
        var recordId = _records.CreateDraft(HealthDeclarations.RecordKind, person, "prof1", null, _time.Now);
        var fields = new Dictionary<string, string> { ["motivo_consulta"] = "Dolor", ["plan"] = "Reposo" };
        var digest = RecordFields.Digest(fields);

        // 1. unknown_record
        var tool1 = CreateTool(fields);
        var r1 = tool1.Execute(new ActionId("guardar_atencion"), new Dictionary<string, string>
        {
            ["record"] = "9999",
            ["by"] = "prof1",
            ["fields_digest"] = digest,
        });
        Assert.Equal("unknown_record", r1.Reason);

        // 2. not_draft: lo firmamos primero para probar
        var signedId = _records.CreateDraft(HealthDeclarations.RecordKind, person, "prof1", null, _time.Now);
        _records.Sign(signedId, "ws", _time.Now);
        var r2 = tool1.Execute(new ActionId("guardar_atencion"), new Dictionary<string, string>
        {
            ["record"] = signedId.ToString(),
            ["by"] = "prof1",
            ["fields_digest"] = digest,
        });
        Assert.Equal("not_draft", r2.Reason);

        // 3. not_author
        var r3 = tool1.Execute(new ActionId("guardar_atencion"), new Dictionary<string, string>
        {
            ["record"] = recordId.ToString(),
            ["by"] = "otro_prof",
            ["fields_digest"] = digest,
        });
        Assert.Equal("not_author", r3.Reason);

        // 4. unknown_field (incluso si viene con un campo válido)
        var badFields = new Dictionary<string, string> { ["motivo_consulta"] = "ok", ["campo_inventado"] = "123" };
        var toolBad = CreateTool(badFields);
        var r4 = toolBad.Execute(new ActionId("guardar_atencion"), new Dictionary<string, string>
        {
            ["record"] = recordId.ToString(),
            ["by"] = "prof1",
            ["fields_digest"] = RecordFields.Digest(badFields),
        });
        Assert.Equal("unknown_field", r4.Reason);

        // 5. bad_digest
        var r5 = tool1.Execute(new ActionId("guardar_atencion"), new Dictionary<string, string>
        {
            ["record"] = recordId.ToString(),
            ["by"] = "prof1",
            ["fields_digest"] = "digest_incorrecto",
        });
        Assert.Equal("bad_digest", r5.Reason);

        // 6. Éxito
        var r6 = tool1.Execute(new ActionId("guardar_atencion"), new Dictionary<string, string>
        {
            ["record"] = recordId.ToString(),
            ["by"] = "prof1",
            ["fields_digest"] = digest,
        });
        Assert.Equal(ToolStatus.Done, r6.Status);
        Assert.Equal(recordId.ToString(), r6.Data["record"]);
    }

    [Fact]
    public void Firmar_atencion_rechaza_por_orden_y_rellena_obligatorios()
    {
        var person = CreateCompletePerson();
        var recordId = _records.CreateDraft(HealthDeclarations.RecordKind, person, "prof1", null, _time.Now);

        // 1. unknown_record
        var tool = CreateTool();
        var r1 = tool.Execute(new ActionId("firmar_atencion"), new Dictionary<string, string>
        {
            ["record"] = "9999",
            ["by"] = "prof1",
        });
        Assert.Equal("unknown_record", r1.Reason);

        // 2. not_draft
        var signedId = _records.CreateDraft(HealthDeclarations.RecordKind, person, "prof1", null, _time.Now);
        _records.Sign(signedId, "ws", _time.Now);
        var r2 = tool.Execute(new ActionId("firmar_atencion"), new Dictionary<string, string>
        {
            ["record"] = signedId.ToString(),
            ["by"] = "prof1",
        });
        Assert.Equal("not_draft", r2.Reason);

        // 3. not_author
        var r3 = tool.Execute(new ActionId("firmar_atencion"), new Dictionary<string, string>
        {
            ["record"] = recordId.ToString(),
            ["by"] = "otro_prof",
        });
        Assert.Equal("not_author", r3.Reason);

        // 4. Éxito: solo guardamos motivo_consulta; los demás obligatorios deben quedar con "No consta."
        _records.SaveDraft(recordId, new Dictionary<string, string> { ["motivo_consulta"] = "Consulta de control" }, _time.Now);
        var r4 = tool.Execute(new ActionId("firmar_atencion"), new Dictionary<string, string>
        {
            ["record"] = recordId.ToString(),
            ["by"] = "prof1",
        });
        Assert.Equal(ToolStatus.Done, r4.Status);

        var signed = _records.Open(recordId);
        Assert.NotNull(signed);
        Assert.Equal(RecordStatus.Signed, signed.Summary.Status);
        Assert.NotNull(signed.ContentHash);
        Assert.Equal("Consulta de control", signed.Fields["motivo_consulta"]);
        Assert.Equal("No consta.", signed.Fields["enfermedad_actual"]);
        Assert.Equal("No consta.", signed.Fields["antecedentes"]);
        Assert.Equal("No consta.", signed.Fields["examen"]);
        Assert.Equal("No consta.", signed.Fields["diagnostico"]);
        Assert.Equal("No consta.", signed.Fields["plan"]);
    }
}
