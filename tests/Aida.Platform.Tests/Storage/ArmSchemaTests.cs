using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Platform.Tests.Storage;

public sealed class ArmSchemaTests
{
    [Fact]
    public void Aplica_los_pasos_que_faltan_y_guarda_la_version()
    {
        using var db = new TestDatabase();
        using var conn = db.Database.Open();

        var steps = new[]
        {
            "CREATE TABLE t1 (id INTEGER PRIMARY KEY);",
            "CREATE TABLE t2 (id INTEGER PRIMARY KEY);",
        };

        ArmSchema.Apply(conn, "brazo_a", steps);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT version FROM arm_schema WHERE arm = 'brazo_a';";
        Assert.Equal(2L, (long)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void Aplicar_dos_veces_no_rehace()
    {
        using var db = new TestDatabase();
        using var conn = db.Database.Open();

        var steps = new[] { "CREATE TABLE t_doble (id INTEGER PRIMARY KEY);" };

        ArmSchema.Apply(conn, "brazo_doble", steps);
        // Si rehiciera el CREATE TABLE t_doble, SQLite lanzaría error de tabla ya existente
        ArmSchema.Apply(conn, "brazo_doble", steps);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT version FROM arm_schema WHERE arm = 'brazo_doble';";
        Assert.Equal(1L, (long)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void Una_version_mas_nueva_no_se_abre()
    {
        using var db = new TestDatabase();
        using var conn = db.Database.Open();

        using (var insert = conn.CreateCommand())
        {
            insert.CommandText = "INSERT INTO arm_schema (arm, version) VALUES ('brazo_futuro', 5);";
            insert.ExecuteNonQuery();
        }

        var steps = new[] { "CREATE TABLE t_futuro (id INTEGER PRIMARY KEY);" };

        var ex = Assert.Throws<InvalidOperationException>(() => ArmSchema.Apply(conn, "brazo_futuro", steps));
        Assert.Contains("el esquema del brazo «brazo_futuro» es de la versión 5 y este AIDA solo conoce hasta la 1", ex.Message);
    }

    [Fact]
    public void Dos_brazos_no_se_pisan()
    {
        using var db = new TestDatabase();
        using var conn = db.Database.Open();

        ArmSchema.Apply(conn, "brazo_1", ["CREATE TABLE b1 (id INTEGER PRIMARY KEY);"]);
        ArmSchema.Apply(conn, "brazo_2", [
            "CREATE TABLE b2_1 (id INTEGER PRIMARY KEY);",
            "CREATE TABLE b2_2 (id INTEGER PRIMARY KEY);",
        ]);

        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = "SELECT version FROM arm_schema WHERE arm = 'brazo_1';";
        Assert.Equal(1L, (long)cmd1.ExecuteScalar()!);

        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "SELECT version FROM arm_schema WHERE arm = 'brazo_2';";
        Assert.Equal(2L, (long)cmd2.ExecuteScalar()!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1invalido")]
    [InlineData("BrazoMayuscula")]
    [InlineData("brazo-con-guion")]
    public void Un_identificador_invalido_o_sin_pasos_lanza(string invalidId)
    {
        using var db = new TestDatabase();
        using var conn = db.Database.Open();

        var steps = new[] { "CREATE TABLE t (id INTEGER PRIMARY KEY);" };
        Assert.Throws<ArgumentException>(() => ArmSchema.Apply(conn, invalidId, steps));
        Assert.Throws<ArgumentException>(() => ArmSchema.Apply(conn, "brazo_valido", []));
    }

    [Fact]
    public void Un_paso_que_falla_no_deja_nada()
    {
        using var db = new TestDatabase();
        using var conn = db.Database.Open();

        var steps = new[]
        {
            "CREATE TABLE t_transitorio (id INTEGER PRIMARY KEY);",
            "ESTE SQL ESTA COMPLETAMENTE ROTO Y DEBE FALLAR;",
        };

        Assert.Throws<SqliteException>(() => ArmSchema.Apply(conn, "brazo_roto", steps));

        // Ni la tabla ni la versión en arm_schema deben quedar
        using var cmdTables = conn.CreateCommand();
        cmdTables.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name = 't_transitorio';";
        Assert.Equal(0L, (long)cmdTables.ExecuteScalar()!);

        using var cmdArm = conn.CreateCommand();
        cmdArm.CommandText = "SELECT count(*) FROM arm_schema WHERE arm = 'brazo_roto';";
        Assert.Equal(0L, (long)cmdArm.ExecuteScalar()!);
    }
}
