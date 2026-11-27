using System.Text;
using Aida.Platform.Keys;
using Aida.Platform.People;
using Aida.Platform.Records;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Platform.Tests.Records;

public sealed class RecordStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _dbPath;
    private readonly PlatformDatabase _db;
    private readonly Sealer _sealer;
    private readonly PersonKeys _keys;
    private readonly DateTimeOffset _now = new(2026, 11, 20, 10, 0, 0, TimeSpan.FromHours(-5));

    public RecordStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "aida-recordstore-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "aida.db");
        _db = new PlatformDatabase(_dbPath);

        var key = MachineKey.OpenOrCreate(Path.Combine(_dir, "machine.key"), new DevelopmentKeyProtector());
        _sealer = new Sealer(key);
        _keys = PersonKeys.From(key);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    private long CreatePerson(SqliteConnection conn, SqliteTransaction tx) =>
        new PersonStore(conn, tx, _sealer, _keys).Add("Persona Test", "3001234567").Id;

    [Fact]
    public void Ciclo_borrador_guardar_y_firmar()
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        var person = CreatePerson(conn, tx);
        var store = new RecordStore(conn, tx, _sealer);

        var id = store.CreateDraft("test_kind", person, "prof-1", "ref-1", _now);
        var initial = store.Open(id);
        Assert.NotNull(initial);
        Assert.Equal(RecordStatus.Draft, initial.Summary.Status);
        Assert.Null(initial.ContentHash);
        Assert.Empty(initial.Fields);

        var fields = new Dictionary<string, string> { ["motivo"] = "Consulta general", ["plan"] = "Reposo" };
        store.SaveDraft(id, fields, _now.AddMinutes(5));

        var updated = store.Open(id);
        Assert.NotNull(updated);
        Assert.Equal(RecordStatus.Draft, updated.Summary.Status);
        Assert.Equal("Consulta general", updated.Fields["motivo"]);
        Assert.Equal("Reposo", updated.Fields["plan"]);

        var signTime = _now.AddMinutes(10);
        var hash = store.Sign(id, "puesto-1", signTime);
        Assert.Equal(64, hash.Length);

        var signed = store.Open(id);
        Assert.NotNull(signed);
        Assert.Equal(RecordStatus.Signed, signed.Summary.Status);
        Assert.Equal(hash, signed.ContentHash);
        Assert.Equal(signTime, signed.Summary.SignedAt);
        Assert.Equal("Consulta general", signed.Fields["motivo"]);
        Assert.Equal("Reposo", signed.Fields["plan"]);
    }

    [Fact]
    public void Guardar_o_firmar_sobre_lo_firmado_lanza_InvalidOperationException()
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        var person = CreatePerson(conn, tx);
        var store = new RecordStore(conn, tx, _sealer);

        var id = store.CreateDraft("test_kind", person, "prof-1", null, _now);
        store.SaveDraft(id, new Dictionary<string, string> { ["campo"] = "valor" }, _now);
        store.Sign(id, "puesto-1", _now.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            store.SaveDraft(id, new Dictionary<string, string> { ["campo"] = "otro" }, _now.AddMinutes(2)));

        Assert.Throws<InvalidOperationException>(() =>
            store.Sign(id, "puesto-1", _now.AddMinutes(3)));
    }

    [Fact]
    public void Update_y_delete_directos_sobre_firmado_fallan_por_trigger_sqlite()
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        var person = CreatePerson(conn, tx);
        var store = new RecordStore(conn, tx, _sealer);

        var id = store.CreateDraft("test_kind", person, "prof-1", null, _now);
        store.Sign(id, "puesto-1", _now.AddMinutes(1));

        using var cmdUpdate = conn.CreateCommand();
        cmdUpdate.Transaction = tx;
        cmdUpdate.CommandText = "UPDATE records SET author = 'intruso' WHERE id = $id;";
        cmdUpdate.Parameters.AddWithValue("$id", id);
        var exUpdate = Assert.Throws<SqliteException>(() => cmdUpdate.ExecuteNonQuery());
        Assert.Contains("records: lo firmado no se cambia", exUpdate.Message);

        using var cmdDelete = conn.CreateCommand();
        cmdDelete.Transaction = tx;
        cmdDelete.CommandText = "DELETE FROM records WHERE id = $id;";
        cmdDelete.Parameters.AddWithValue("$id", id);
        var exDelete = Assert.Throws<SqliteException>(() => cmdDelete.ExecuteNonQuery());
        Assert.Contains("records: lo firmado no se borra", exDelete.Message);
    }

    [Fact]
    public void ListFor_devuelve_resumenes_de_la_persona_en_orden()
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        var person1 = CreatePerson(conn, tx);
        var person2 = CreatePerson(conn, tx);
        var store = new RecordStore(conn, tx, _sealer);

        var id1 = store.CreateDraft("kind_a", person1, "prof-1", null, _now);
        var id2 = store.CreateDraft("kind_a", person1, "prof-2", null, _now.AddHours(1));
        var id3 = store.CreateDraft("kind_b", person1, "prof-1", null, _now.AddHours(2));
        var idOther = store.CreateDraft("kind_a", person2, "prof-1", null, _now);

        var listA = store.ListFor(person1, "kind_a");
        Assert.Equal(2, listA.Count);
        Assert.Equal(id2, listA[0].Id);
        Assert.Equal(id1, listA[1].Id);

        var listB = store.ListFor(person1, "kind_b");
        Assert.Single(listB);
        Assert.Equal(id3, listB[0].Id);

        var listOther = store.ListFor(person2, "kind_a");
        Assert.Single(listOther);
        Assert.Equal(idOther, listOther[0].Id);
    }

    [Fact]
    public void En_los_bytes_de_records_no_aparece_el_texto_de_ningun_campo()
    {
        const string secreto = "DiagnosticoPrivado987654321";
        using (var conn = _db.Open())
        using (var tx = conn.BeginTransaction())
        {
            var person = CreatePerson(conn, tx);
            var store = new RecordStore(conn, tx, _sealer);
            var id = store.CreateDraft("test_kind", person, "prof-1", null, _now);
            store.SaveDraft(id, new Dictionary<string, string> { ["motivo"] = secreto }, _now);
            store.Sign(id, "puesto-1", _now.AddMinutes(2));
            tx.Commit();
        }

        using (var conn = _db.Open())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT content FROM records;";
            var blob = (byte[])cmd.ExecuteScalar()!;
            var blobText = Encoding.UTF8.GetString(blob);
            Assert.DoesNotContain(secreto, blobText);
        }

        SqliteConnection.ClearAllPools();
        var dbBytes = File.ReadAllBytes(_dbPath);
        Assert.DoesNotContain(Encoding.UTF8.GetBytes(secreto), dbBytes);
    }
}
