using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Storage;
using Aida.Platform.Keys;
using Aida.Platform.People;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Actions;

public sealed class ClientSearchTests : IDisposable
{
    private readonly string _dir;
    private readonly PlatformDatabase _db;
    private readonly MachineKey _key;
    private readonly Sealer _sealer;
    private readonly PersonKeys _personKeys;

    public ClientSearchTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "aida-clientsearch-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        var dbPath = Path.Combine(_dir, "aida.db");
        _db = new PlatformDatabase(dbPath);

        var keyPath = Path.Combine(_dir, "machine.key");
        _key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
        _sealer = new Sealer(_key);
        _personKeys = PersonKeys.From(_key);
        _db.ApplyArmSchema(AgendaDeclarations.ArmId, AgendaSchema.Steps(_sealer, _personKeys));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir))
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void Busqueda_por_telefono_normaliza_entrada_y_guardado()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        work.People.Add("Ana Pérez", "300 555 0142");
        work.Commit();

        using var queryWork = AgendaWork.Begin(_db, _sealer, _personKeys);
        // Búsqueda sin espacios encuentra el teléfono guardado con espacios (D4)
        var exactos = ClientSearch.Find(queryWork.People, "3005550142");
        var exacto = Assert.Single(exactos);
        Assert.Equal("Ana Pérez", exacto.Name);
        Assert.Equal("300 555 0142", exacto.Phone);

        // Búsqueda de sufijo con espacios también encuentra
        var parciales = ClientSearch.Find(queryWork.People, "555 0142");
        var parcial = Assert.Single(parciales);
        Assert.Equal("Ana Pérez", parcial.Name);
    }

    [Fact]
    public void Busqueda_con_letras_busca_por_nombre_aunque_lleve_digitos()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        work.People.Add("Ana 3", "3100000000");
        work.People.Add("Carlos", "3000000003");
        work.Commit();

        using var queryWork = AgendaWork.Begin(_db, _sealer, _personKeys);
        var encontrados = ClientSearch.Find(queryWork.People, "ana 3");
        var encontrado = Assert.Single(encontrados);
        Assert.Equal("Ana 3", encontrado.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("--")]
    [InlineData("+ . -")]
    public void Busqueda_vacia_o_solo_signos_devuelve_vacio(string consulta)
    {
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        work.People.Add("Ana Pérez", "3005550142");
        work.Commit();

        using var queryWork = AgendaWork.Begin(_db, _sealer, _personKeys);
        var encontrados = ClientSearch.Find(queryWork.People, consulta);
        Assert.Empty(encontrados);
    }

    [Fact]
    public void Busqueda_limita_a_20_resultados()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        for (var i = 1; i <= 25; i++)
        {
            work.People.Add($"Paciente {i:D2}", $"30000000{i:D2}");
        }

        work.Commit();

        using var queryWork = AgendaWork.Begin(_db, _sealer, _personKeys);
        var encontrados = ClientSearch.Find(queryWork.People, "Paciente");
        Assert.Equal(20, encontrados.Count);
    }

    [Fact]
    public void Busqueda_desempata_por_id_a_igual_nombre_plegado()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _personKeys);
        var c1 = work.People.Add("Ana Gómez", "3001111111");
        var c2 = work.People.Add("Ána Gomez", "3002222222");
        work.Commit();

        using var queryWork = AgendaWork.Begin(_db, _sealer, _personKeys);
        var encontrados = ClientSearch.Find(queryWork.People, "ana gomez");
        Assert.Equal(2, encontrados.Count);
        Assert.Equal(c1.Id, encontrados[0].Id);
        Assert.Equal(c2.Id, encontrados[1].Id);
    }
}
