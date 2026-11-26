using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;
using Aida.Platform.Keys;
using Aida.Platform.People;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Actions;

public sealed class ContactDetailsTests : IDisposable
{
    private readonly string _dir;
    private readonly PlatformDatabase _db;
    private readonly MachineKey _key;
    private readonly Sealer _sealer;
    private readonly PersonKeys _personKeys;
    private readonly PersonCatalog _catalog;
    private readonly DateOnly _today = new(2026, 11, 26);

    public ContactDetailsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "aida-contactdetails-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        _db = new PlatformDatabase(Path.Combine(_dir, "aida.db"));
        _key = MachineKey.OpenOrCreate(Path.Combine(_dir, "machine.key"), new DevelopmentKeyProtector());
        _sealer = new Sealer(_key);
        _personKeys = PersonKeys.From(_key);
        _db.ApplyArmSchema(AgendaDeclarations.ArmId, AgendaSchema.Steps(_sealer, _personKeys));

        var repoRoot = FindRepoRoot();
        _catalog = PersonCatalog.Load(Path.Combine(repoRoot, "packs", "platform", "people.yaml"));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }
        catch
        {
        }
    }

    private AgendaWork Begin() => AgendaWork.Begin(_db, _sealer, _personKeys);

    [Fact]
    public void Omision_de_campos_de_documento_conserva_el_existente()
    {
        using var work = Begin();
        var p = work.People.Add("Persona", "3001234567");
        var original = new PersonDetails("CC", "1234567", new DateOnly(1995, 1, 1), "H");
        work.People.UpdateDetails(p.Id, original);
        work.Commit();

        using var testWork = Begin();
        var current = testWork.People.Find(p.Id)!;
        var args = new Dictionary<string, string> { ["sex"] = "M" };
        var rej = ContactDetails.Merge(testWork.People, _catalog, _today, current, args, out var updated);
        Assert.Null(rej);
        Assert.Equal("CC", updated.DocumentType);
        Assert.Equal("1234567", updated.DocumentNumber);
        Assert.Equal("M", updated.Sex);
    }

    [Fact]
    public void Longitudes_limite_3_y_20_son_validas_mas_de_20_o_no_alfanumerico_rechaza()
    {
        using var work = Begin();
        var p = work.People.Add("Persona", "3001234567");
        work.Commit();

        using var testWork = Begin();
        var current = testWork.People.Find(p.Id)!;

        // Longitud 3 (válida)
        var rej3 = ContactDetails.Merge(testWork.People, _catalog, _today, current, new Dictionary<string, string>
        {
            ["document_type"] = "CC", ["document_number"] = "123",
        }, out var up3);
        Assert.Null(rej3);
        Assert.Equal("123", up3.DocumentNumber);

        // Longitud 20 (válida)
        var doc20 = new string('A', 20);
        var rej20 = ContactDetails.Merge(testWork.People, _catalog, _today, current, new Dictionary<string, string>
        {
            ["document_type"] = "PA", ["document_number"] = doc20,
        }, out var up20);
        Assert.Null(rej20);
        Assert.Equal(doc20, up20.DocumentNumber);

        // Longitud 21 (rechazada)
        var rej21 = ContactDetails.Merge(testWork.People, _catalog, _today, current, new Dictionary<string, string>
        {
            ["document_type"] = "PA", ["document_number"] = new string('B', 21),
        }, out _);
        Assert.Equal("bad_document", rej21?.Reason);

        // No alfanumérico (rechazada)
        var rejSimbolo = ContactDetails.Merge(testWork.People, _catalog, _today, current, new Dictionary<string, string>
        {
            ["document_type"] = "CC", ["document_number"] = "123?456",
        }, out _);
        Assert.Equal("bad_document", rejSimbolo?.Reason);
    }

    [Fact]
    public void Documento_con_uno_vacio_y_otro_lleno_devuelve_incomplete_document()
    {
        using var work = Begin();
        var p = work.People.Add("Persona", "3001234567");
        work.Commit();

        using var testWork = Begin();
        var current = testWork.People.Find(p.Id)!;

        var r1 = ContactDetails.Merge(testWork.People, _catalog, _today, current, new Dictionary<string, string>
        {
            ["document_type"] = "CC", ["document_number"] = "   ",
        }, out _);
        Assert.Equal("incomplete_document", r1?.Reason);

        var r2 = ContactDetails.Merge(testWork.People, _catalog, _today, current, new Dictionary<string, string>
        {
            ["document_type"] = "   ", ["document_number"] = "1234567",
        }, out _);
        Assert.Equal("incomplete_document", r2?.Reason);
    }

    [Fact]
    public void Fecha_nacimiento_hoy_se_acepta_anterior_a_1900_o_formato_invalido_se_rechaza()
    {
        using var work = Begin();
        var p = work.People.Add("Persona", "3001234567");
        work.Commit();

        using var testWork = Begin();
        var current = testWork.People.Find(p.Id)!;

        // Hoy (válida)
        var rejHoy = ContactDetails.Merge(testWork.People, _catalog, _today, current, new Dictionary<string, string>
        {
            ["birth_date"] = _today.ToString("yyyy-MM-dd"),
        }, out var upHoy);
        Assert.Null(rejHoy);
        Assert.Equal(_today, upHoy.BirthDate);

        // Anterior a 1900 (rechazada)
        var rejVieja = ContactDetails.Merge(testWork.People, _catalog, _today, current, new Dictionary<string, string>
        {
            ["birth_date"] = "1899-12-31",
        }, out _);
        Assert.Equal("bad_birth_date", rejVieja?.Reason);

        // Formato no parseable (rechazada)
        var rejInvalida = ContactDetails.Merge(testWork.People, _catalog, _today, current, new Dictionary<string, string>
        {
            ["birth_date"] = "26/11/2026",
        }, out _);
        Assert.Equal("bad_birth_date", rejInvalida?.Reason);
    }
}
