using Aida.Platform.Ledger;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Platform.Tests.Ledger;

public sealed class ChainedLogTests
{
    [Fact]
    public void La_primera_linea_encadena_con_la_genesis()
    {
        using var db = new TestDatabase();
        using var connection = db.Database.Open();
        using var transaction = connection.BeginTransaction();

        var digest = ChainedLog.Append(
            connection,
            transaction,
            new Dictionary<string, string> { ["msg"] = "primera" });
        transaction.Commit();

        var lines = ChainedLog.Lines(connection);
        Assert.Single(lines);
        Assert.Contains($"\"prev\": \"{ChainedLog.Genesis}\"", lines[0]);
        Assert.Contains($"\"digest\": \"{digest}\"", lines[0]);

        var problems = ChainedLog.Verify(connection);
        Assert.Empty(problems);
    }

    [Fact]
    public void Prev_y_digest_los_pone_la_cadena()
    {
        using var db = new TestDatabase();
        using var connection = db.Database.Open();
        using var transaction = connection.BeginTransaction();

        var exPrev = Assert.Throws<ArgumentException>(() =>
            ChainedLog.Append(connection, transaction, new Dictionary<string, string> { ["prev"] = "otro" }));
        Assert.Contains("'prev' y 'digest' los pone la cadena, no quien añade", exPrev.Message);

        var exDigest = Assert.Throws<ArgumentException>(() =>
            ChainedLog.Append(connection, transaction, new Dictionary<string, string> { ["digest"] = "otro" }));
        Assert.Contains("'prev' y 'digest' los pone la cadena, no quien añade", exDigest.Message);
    }

    [Fact]
    public void Cambiar_una_linea_se_detecta_con_el_mensaje_de_la_01()
    {
        using var db = new TestDatabase();
        using var connection = db.Database.Open();

        using (var t1 = connection.BeginTransaction())
        {
            ChainedLog.Append(connection, t1, new Dictionary<string, string> { ["v"] = "1" });
            t1.Commit();
        }

        using (var t2 = connection.BeginTransaction())
        {
            ChainedLog.Append(connection, t2, new Dictionary<string, string> { ["v"] = "2" });
            t2.Commit();
        }

        // Modificamos el contenido de la línea 2 sin cambiar su digest en la tabla
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE ledger SET line = replace(line, '\"v\": \"2\"', '\"v\": \"alterado\"') WHERE seq = 2;";
            cmd.ExecuteNonQuery();
        }

        var problems = ChainedLog.Verify(connection);
        Assert.Single(problems);
        Assert.Equal("línea 2: su contenido no corresponde a su huella", problems[0]);
    }

    [Fact]
    public void Borrar_una_linea_rompe_la_siguiente()
    {
        using var db = new TestDatabase();
        using var connection = db.Database.Open();

        for (var i = 1; i <= 3; i++)
        {
            using var t = connection.BeginTransaction();
            ChainedLog.Append(connection, t, new Dictionary<string, string> { ["n"] = i.ToString() });
            t.Commit();
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM ledger WHERE seq = 2;";
            cmd.ExecuteNonQuery();
        }

        var problems = ChainedLog.Verify(connection);
        Assert.Single(problems);
        Assert.Equal("línea 2: no sigue a la anterior", problems[0]);
    }

    [Fact]
    public void Una_linea_ilegible_se_dice()
    {
        using var db = new TestDatabase();
        using var connection = db.Database.Open();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO ledger (digest, line) VALUES ('fake', 'no-es-json');";
            cmd.ExecuteNonQuery();
        }

        var problems = ChainedLog.Verify(connection);
        Assert.Single(problems);
        Assert.Equal("línea 1: no se puede leer", problems[0]);
    }

    [Fact]
    public void Cuarenta_escrituras_paralelas_dejan_la_cadena_integra()
    {
        using var db = new TestDatabase();

        Parallel.For(0, 40, i =>
        {
            using var conn = db.Database.Open();
            using var tx = conn.BeginTransaction();
            ChainedLog.Append(conn, tx, new Dictionary<string, string> { ["indice"] = i.ToString() });
            tx.Commit();
        });

        using var verifyConn = db.Database.Open();
        var lines = ChainedLog.Lines(verifyConn);
        Assert.Equal(40, lines.Count);

        var problems = ChainedLog.Verify(verifyConn);
        Assert.Empty(problems);
    }
}
