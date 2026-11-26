using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Setup;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;
using Aida.Engine.Ontology;
using Aida.Platform.Keys;
using Aida.Platform.Packs;
using Aida.Platform.People;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Actions;

public sealed class BookClientTests : IDisposable
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    private readonly string _dir;
    private readonly PlatformDatabase _db;
    private readonly MachineKey _key;
    private readonly Sealer _sealer;
    private readonly PersonKeys _personKeys;
    private readonly PersonCatalog _catalog;
    private readonly AgendaSetup _setup;
    private readonly ManualTime _time;

    public BookClientTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "aida-bookclient-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        var dbPath = Path.Combine(_dir, "aida.db");
        _db = new PlatformDatabase(dbPath);

        var keyPath = Path.Combine(_dir, "machine.key");
        _key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
        _sealer = new Sealer(_key);
        _personKeys = PersonKeys.From(_key);
        _db.ApplyArmSchema(AgendaDeclarations.ArmId, AgendaSchema.Steps(_sealer, _personKeys));

        var repoRoot = FindRepoRoot();
        _catalog = PersonCatalog.Load(Path.Combine(repoRoot, "packs", "platform", "people.yaml"));
        var consultorioPath = Path.Combine(repoRoot, "packs", "agenda", "consultorio", "agenda.yaml");
        _setup = AgendaSetupLoader.Parse(PlatformYaml.ReadRoot(consultorioPath), consultorioPath);

        _time = new ManualTime { Now = DateTimeOffset.Parse("2026-11-16T07:00:00-05:00") };
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

    private ToolResult ExecuteBook(AgendaWork work, string? client, string name, string phone, string start = "2026-11-16T08:00:00-05:00")
    {
        var tool = new AgendaTool(work.Store, work.People, _setup, _catalog, _time);
        var args = new Dictionary<string, string>
        {
            ["name"] = name,
            ["phone"] = phone,
            ["service"] = "control",
            ["resource"] = "consultorio_1",
            ["start"] = start,
            ["by"] = "u1",
        };
        if (client is not null)
        {
            args["client"] = client;
        }

        return tool.Execute(new ActionId("agendar"), args);
    }

    [Fact]
    public void Sin_client_telefono_nuevo_crea_persona()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        var res = ExecuteBook(work, client: null, name: "Nuevo Cliente", phone: "3001110000");
        Assert.Equal(ToolStatus.Done, res.Status);
        work.Commit();

        using var query = AgendaWork.Begin(_db, _sealer, _personKeys);
        var found = query.People.ByPhone("3001110000");
        var person = Assert.Single(found);
        Assert.Equal("Nuevo Cliente", person.Name);
    }

    [Fact]
    public void Sin_client_telefono_de_una_persona_la_usa_y_renombra()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        var initial = work.People.Add("Ana Original", "3002220000");
        work.Commit();

        using var workBook = AgendaWork.Begin(_db, _sealer, _personKeys);
        var res = ExecuteBook(workBook, client: null, name: "Ana Renombrada", phone: "3002220000");
        Assert.Equal(ToolStatus.Done, res.Status);
        workBook.Commit();

        using var query = AgendaWork.Begin(_db, _sealer, _personKeys);
        var found = query.People.ByPhone("3002220000");
        var person = Assert.Single(found);
        Assert.Equal(initial.Id, person.Id);
        Assert.Equal("Ana Renombrada", person.Name);
    }

    [Fact]
    public void Sin_client_telefono_de_dos_personas_devuelve_client_ambiguous()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        work.People.Add("Persona Uno", "3003330000");
        work.People.Add("Persona Dos", "3003330000");
        work.Commit();

        using var workBook = AgendaWork.Begin(_db, _sealer, _personKeys);
        var res = ExecuteBook(workBook, client: null, name: "Persona Tres", phone: "3003330000");
        Assert.Equal(ToolStatus.Rejected, res.Status);
        Assert.Equal("client_ambiguous", res.Reason);
    }

    [Fact]
    public void Client_new_con_telefono_de_madre_y_nombre_de_hijo_crea_al_hijo_y_madre_no_cambia()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        var madre = work.People.Add("Madre", "3004440000");
        work.Commit();

        using var workBook = AgendaWork.Begin(_db, _sealer, _personKeys);
        var res = ExecuteBook(workBook, client: "new", name: "Niño", phone: "3004440000");
        Assert.Equal(ToolStatus.Done, res.Status);
        workBook.Commit();

        using var query = AgendaWork.Begin(_db, _sealer, _personKeys);
        var samePhone = query.People.ByPhone("3004440000");
        Assert.Equal(2, samePhone.Count);
        Assert.Equal("Madre", samePhone[0].Name);
        Assert.Equal("Niño", samePhone[1].Name);
        Assert.Equal(madre.Id, samePhone[0].Id);
    }

    [Fact]
    public void Client_new_mismo_telefono_y_mismo_nombre_plegado_reutiliza_persona()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        var existente = work.People.Add("Ana Pérez", "3005550000");
        work.Commit();

        using var workBook = AgendaWork.Begin(_db, _sealer, _personKeys);
        // «ana perez» sin tildes casa con «Ana Pérez»
        var res = ExecuteBook(workBook, client: "new", name: "ana perez", phone: "3005550000");
        Assert.Equal(ToolStatus.Done, res.Status);
        workBook.Commit();

        using var query = AgendaWork.Begin(_db, _sealer, _personKeys);
        var people = query.People.ByPhone("3005550000");
        // M2: Si no se busca por nombre plegado, se habrían creado 2 personas. Debe haber exactamente 1.
        var person = Assert.Single(people);
        Assert.Equal(existente.Id, person.Id);
    }

    [Fact]
    public void Client_id_usa_esa_persona_sin_renombrar()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        var persona = work.People.Add("Carlos Original", "3006660000");
        work.Commit();

        using var workBook = AgendaWork.Begin(_db, _sealer, _personKeys);
        var res = ExecuteBook(workBook, client: persona.Id.ToString(), name: "Nombre Ignorado Del Formulario", phone: "3006660000");
        Assert.Equal(ToolStatus.Done, res.Status);
        workBook.Commit();

        using var query = AgendaWork.Begin(_db, _sealer, _personKeys);
        var cargada = query.People.Find(persona.Id);
        Assert.NotNull(cargada);
        Assert.Equal("Carlos Original", cargada.Name);
    }

    [Fact]
    public void Client_desconocido_devuelve_unknown_client()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        var res = ExecuteBook(work, client: "99", name: "Fantasma", phone: "3007770000");
        Assert.Equal(ToolStatus.Rejected, res.Status);
        Assert.Equal("unknown_client", res.Reason);
    }
}
