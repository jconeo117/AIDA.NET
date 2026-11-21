using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Storage;
using Aida.Platform.Keys;
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
    private readonly byte[] _phoneKey;

    public ClientSearchTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "aida-clientsearch-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        var dbPath = Path.Combine(_dir, "aida.db");
        _db = new PlatformDatabase(dbPath);
        _db.ApplyArmSchema(AgendaDeclarations.ArmId, AgendaSchema.Steps);

        var keyPath = Path.Combine(_dir, "machine.key");
        _key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
        _sealer = new Sealer(_key);
        _phoneKey = _key.Derive(AgendaStore.PhoneKeyPurpose);
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
        using var work = AgendaWork.Begin(_db, _sealer, _phoneKey);
        work.Store.AddClient("Ana Pérez", "300 555 0142");
        work.Commit();

        using var queryWork = AgendaWork.Begin(_db, _sealer, _phoneKey);
        // Búsqueda sin espacios encuentra el teléfono guardado con espacios (D4)
        var exactos = ClientSearch.Find(queryWork.Store, "3005550142");
        var exacto = Assert.Single(exactos);
        Assert.Equal("Ana Pérez", exacto.Name);
        Assert.Equal("300 555 0142", exacto.Phone);

        // Búsqueda de sufijo con espacios también encuentra
        var parciales = ClientSearch.Find(queryWork.Store, "555 0142");
        var parcial = Assert.Single(parciales);
        Assert.Equal("Ana Pérez", parcial.Name);
    }

    [Fact]
    public void Busqueda_con_letras_busca_por_nombre_aunque_lleve_digitos()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _phoneKey);
        work.Store.AddClient("Ana 3", "3100000000");
        work.Store.AddClient("Carlos", "3000000003");
        work.Commit();

        using var queryWork = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var encontrados = ClientSearch.Find(queryWork.Store, "ana 3");
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
        using var work = AgendaWork.Begin(_db, _sealer, _phoneKey);
        work.Store.AddClient("Ana Pérez", "3005550142");
        work.Commit();

        using var queryWork = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var encontrados = ClientSearch.Find(queryWork.Store, consulta);
        Assert.Empty(encontrados);
    }

    [Fact]
    public void Busqueda_limita_a_20_resultados()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _phoneKey);
        for (var i = 1; i <= 25; i++)
        {
            work.Store.AddClient($"Paciente {i:D2}", $"30000000{i:D2}");
        }

        work.Commit();

        using var queryWork = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var encontrados = ClientSearch.Find(queryWork.Store, "Paciente");
        Assert.Equal(20, encontrados.Count);
    }

    [Fact]
    public void Busqueda_desempata_por_id_a_igual_nombre_plegado()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var c1 = work.Store.AddClient("Ana Gómez", "3001111111");
        var c2 = work.Store.AddClient("Ána Gomez", "3002222222");
        work.Commit();

        using var queryWork = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var encontrados = ClientSearch.Find(queryWork.Store, "ana gomez");
        Assert.Equal(2, encontrados.Count);
        Assert.Equal(c1.Id, encontrados[0].Id);
        Assert.Equal(c2.Id, encontrados[1].Id);
    }
}
