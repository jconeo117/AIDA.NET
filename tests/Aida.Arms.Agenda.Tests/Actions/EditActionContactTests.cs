using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;
using Aida.Platform.Keys;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Actions;

public sealed class EditActionContactTests : IDisposable
{
    private readonly string _dir;
    private readonly PlatformDatabase _db;
    private readonly MachineKey _key;
    private readonly Sealer _sealer;
    private readonly byte[] _phoneKey;

    public EditActionContactTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "aida-editcontact-" + Guid.NewGuid().ToString("n"));
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

    [Theory]
    [InlineData("")]
    [InlineData("invalido")]
    [InlineData("999")]
    public void Contacto_inexistente_o_invalido_devuelve_unknown_client(string clientVal)
    {
        using var work = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var args = new Dictionary<string, string>
        {
            ["client"] = clientVal,
            ["name"] = "Carlos",
            ["phone"] = "3001234567",
        };

        var result = EditAction.Contact(work.Store, args);
        Assert.Equal(ToolStatus.Rejected, result.Status);
        Assert.Equal("unknown_client", result.Reason);
    }

    [Theory]
    [InlineData("", "3001234567")]
    [InlineData("   ", "3001234567")]
    [InlineData("Carlos", "")]
    [InlineData("Carlos", "   ")]
    public void Nombre_o_telefono_vacio_o_blancos_devuelve_missing_client(string name, string phone)
    {
        using var work = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var c = work.Store.AddClient("Carlos", "3001234567");
        work.Commit();

        using var testWork = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var args = new Dictionary<string, string>
        {
            ["client"] = c.Id.ToString(),
            ["name"] = name,
            ["phone"] = phone,
        };

        var result = EditAction.Contact(testWork.Store, args);
        Assert.Equal(ToolStatus.Rejected, result.Status);
        Assert.Equal("missing_client", result.Reason);
    }

    [Fact]
    public void Telefono_duplicado_de_otro_contacto_devuelve_phone_taken()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var c1 = work.Store.AddClient("Carlos", "3001111111");
        var c2 = work.Store.AddClient("Diana", "3002222222");
        work.Commit();

        using var testWork = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var args = new Dictionary<string, string>
        {
            ["client"] = c2.Id.ToString(),
            ["name"] = "Diana",
            ["phone"] = "3001111111", // Teléfono de Carlos
        };

        var result = EditAction.Contact(testWork.Store, args);
        Assert.Equal(ToolStatus.Rejected, result.Status);
        Assert.Equal("phone_taken", result.Reason);
    }

    [Fact]
    public void Mismo_telefono_del_mismo_contacto_es_aceptado()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var c1 = work.Store.AddClient("Carlos", "3001111111");
        work.Commit();

        using var testWork = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var args = new Dictionary<string, string>
        {
            ["client"] = c1.Id.ToString(),
            ["name"] = "Carlos Andrés",
            ["phone"] = "3001111111",
        };

        var result = EditAction.Contact(testWork.Store, args);
        Assert.Equal(ToolStatus.Done, result.Status);
        Assert.Equal(c1.Id.ToString(), result.Data["client"]);
    }

    [Fact]
    public void Edicion_guarda_campos_recortados_y_retorna_done()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var c = work.Store.AddClient("Carlos", "3001111111");
        work.Commit();

        using var testWork = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var args = new Dictionary<string, string>
        {
            ["client"] = c.Id.ToString(),
            ["name"] = "  Carlos Andrés  ",
            ["phone"] = "  3009999999  ",
        };

        var result = EditAction.Contact(testWork.Store, args);
        testWork.Commit();

        Assert.Equal(ToolStatus.Done, result.Status);
        Assert.Equal(c.Id.ToString(), result.Data["client"]);

        using var queryWork = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var updated = queryWork.Store.GetClient(c.Id);
        Assert.Equal("Carlos Andrés", updated.Name);
        Assert.Equal("3009999999", updated.Phone);
    }

    [Fact]
    public void Cambio_de_telefono_mueve_la_huella_en_el_almacen()
    {
        using var work = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var c = work.Store.AddClient("Carlos", "3001111111");
        work.Commit();

        using var testWork = AgendaWork.Begin(_db, _sealer, _phoneKey);
        var args = new Dictionary<string, string>
        {
            ["client"] = c.Id.ToString(),
            ["name"] = "Carlos",
            ["phone"] = "3002222222",
        };

        var result = EditAction.Contact(testWork.Store, args);
        testWork.Commit();
        Assert.Equal(ToolStatus.Done, result.Status);

        using var queryWork = AgendaWork.Begin(_db, _sealer, _phoneKey);
        // La búsqueda por el teléfono anterior ya no encuentra nada
        Assert.Null(queryWork.Store.ClientByPhone("3001111111"));

        // La búsqueda por el nuevo teléfono encuentra al contacto actualizado
        var nuevo = queryWork.Store.ClientByPhone("3002222222");
        Assert.NotNull(nuevo);
        Assert.Equal(c.Id, nuevo.Id);
    }
}
