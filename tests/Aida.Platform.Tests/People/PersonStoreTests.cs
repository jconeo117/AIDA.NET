using System.Text;
using Aida.Platform.Keys;
using Aida.Platform.People;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Platform.Tests.People;

public sealed class PersonStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _dbPath;
    private readonly PlatformDatabase _db;
    private readonly MachineKey _key;
    private readonly Sealer _sealer;
    private readonly PersonKeys _keys;

    public PersonStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "aida-personstore-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "aida.db");
        _db = new PlatformDatabase(_dbPath);

        var keyPath = Path.Combine(_dir, "machine.key");
        _key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
        _sealer = new Sealer(_key);
        _keys = PersonKeys.From(_key);
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

    [Fact]
    public void Alta_y_busqueda_por_telefono_devuelve_todas_por_id()
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction(deferred: false);
        var store = new PersonStore(conn, tx, _sealer, _keys);

        var p1 = store.Add("Madre", "300 555 0142");
        var p2 = store.Add("Hijo", "300.555.0142");
        tx.Commit();

        using var conn2 = _db.Open();
        using var tx2 = conn2.BeginTransaction(deferred: false);
        var store2 = new PersonStore(conn2, tx2, _sealer, _keys);

        var found = store2.ByPhone("3005550142");
        Assert.Equal(2, found.Count);
        Assert.Equal(p1.Id, found[0].Id);
        Assert.Equal("Madre", found[0].Name);
        Assert.Equal(p2.Id, found[1].Id);
        Assert.Equal("Hijo", found[1].Name);
    }

    [Fact]
    public void Busqueda_por_documento_normalizado_encuentra_la_persona()
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction(deferred: false);
        var store = new PersonStore(conn, tx, _sealer, _keys);

        var p = store.Add("Ana", "3001112233");
        store.UpdateDetails(p.Id, new PersonDetails("CC", "1.234.567", new DateOnly(1985, 3, 15), "M"));
        tx.Commit();

        using var conn2 = _db.Open();
        using var tx2 = conn2.BeginTransaction(deferred: false);
        var store2 = new PersonStore(conn2, tx2, _sealer, _keys);

        // Búsqueda sin puntos encuentra el documento guardado con puntos
        var found = store2.ByDocument("CC", "1234567");
        Assert.NotNull(found);
        Assert.Equal(p.Id, found.Id);
        Assert.Equal("CC", found.Details.DocumentType);
        Assert.Equal("1.234.567", found.Details.DocumentNumber);
        Assert.Equal(new DateOnly(1985, 3, 15), found.Details.BirthDate);
        Assert.Equal("M", found.Details.Sex);

        // Documento o tipo distinto no hace colisión
        Assert.Null(store2.ByDocument("TI", "1234567"));
        Assert.Null(store2.ByDocument("CC", "9999999"));
    }

    [Fact]
    public void Documento_y_claves_rechazan_argumentos_nulos()
    {
        Assert.Throws<ArgumentNullException>(() => PersonDocument.Normalize(null!));
        Assert.Throws<ArgumentNullException>(() => PersonKeys.From(null!));
    }

    [Fact]
    public void Import_conserva_el_id_dado_para_migraciones()
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction(deferred: false);
        var store = new PersonStore(conn, tx, _sealer, _keys);

        var p42 = store.Import(42, "Importado", "3100000000");
        tx.Commit();

        Assert.Equal(42, p42.Id);

        using var conn2 = _db.Open();
        using var tx2 = conn2.BeginTransaction(deferred: false);
        var store2 = new PersonStore(conn2, tx2, _sealer, _keys);
        var loaded = store2.Find(42);
        Assert.NotNull(loaded);
        Assert.Equal("Importado", loaded.Name);
        Assert.Equal("3100000000", loaded.Phone);
    }

    [Fact]
    public void Documento_repetido_lanza_sqlite_exception_de_unique()
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction(deferred: false);
        var store = new PersonStore(conn, tx, _sealer, _keys);

        var p1 = store.Add("Uno", "3001111111");
        var p2 = store.Add("Dos", "3002222222");

        store.UpdateDetails(p1.Id, new PersonDetails("CC", "12345678", null, null));
        var ex = Assert.Throws<SqliteException>(() =>
            store.UpdateDetails(p2.Id, new PersonDetails("CC", "12345678", null, null)));

        Assert.Equal(19, ex.SqliteErrorCode); // SQLITE_CONSTRAINT
    }

    [Fact]
    public void Sellado_ningun_dato_personal_aparece_en_claro_en_la_tabla_people()
    {
        const string name = "Paciente Ultra Confidencial";
        const string phone = "3159998877";
        const string docNum = "987654321";
        const string birthStr = "1992-07-24";
        const string sex = "H";

        using (var conn = _db.Open())
        using (var tx = conn.BeginTransaction(deferred: false))
        {
            var store = new PersonStore(conn, tx, _sealer, _keys);
            var p = store.Add(name, phone);
            store.UpdateDetails(p.Id, new PersonDetails("CC", docNum, DateOnly.Parse(birthStr), sex));
            tx.Commit();
        }

        SqliteConnection.ClearAllPools();

        var dbBytes = File.ReadAllBytes(_dbPath);
        Assert.DoesNotContain(Encoding.UTF8.GetBytes(name), dbBytes);
        Assert.DoesNotContain(Encoding.UTF8.GetBytes(phone), dbBytes);
        Assert.DoesNotContain(Encoding.UTF8.GetBytes(docNum), dbBytes);
        Assert.DoesNotContain(Encoding.UTF8.GetBytes(birthStr), dbBytes);

        // El tipo de documento sí va en claro (ADR 0029, único campo sin sellar)
        Assert.Contains(Encoding.UTF8.GetBytes("CC"), dbBytes);
    }

    [Fact]
    public void All_devuelve_todas_las_personas_ordenadas_por_id()
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction(deferred: false);
        var store = new PersonStore(conn, tx, _sealer, _keys);

        store.Add("Primero", "3001110001");
        store.Add("Segundo", "3001110002");
        tx.Commit();

        using var conn2 = _db.Open();
        using var tx2 = conn2.BeginTransaction(deferred: false);
        var store2 = new PersonStore(conn2, tx2, _sealer, _keys);

        var all = store2.All();
        Assert.Equal(2, all.Count);
        Assert.True(all[0].Id < all[1].Id);
    }
}
