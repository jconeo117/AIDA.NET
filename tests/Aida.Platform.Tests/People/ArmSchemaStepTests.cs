using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Platform.Tests.People;

public sealed class ArmSchemaStepTests : IDisposable
{
    private readonly string _dir;
    private readonly string _dbPath;
    private readonly PlatformDatabase _db;

    public ArmSchemaStepTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "aida-armschemastep-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "aida.db");
        _db = new PlatformDatabase(_dbPath);
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
    public void Pasos_de_sql_y_codigo_se_aplican_en_orden()
    {
        var log = new List<string>();
        IReadOnlyList<ArmSchemaStep> steps =
        [
            ArmSchemaStep.Sql("CREATE TABLE t1 (id INT);"),
            ArmSchemaStep.Code((conn, tx) =>
            {
                log.Add("code-step");
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO t1 VALUES (10);";
                cmd.ExecuteNonQuery();
            }),
            ArmSchemaStep.Sql("CREATE TABLE t2 (id INT);"),
        ];

        _db.ApplyArmSchema("brazo", steps);

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT (SELECT count(*) FROM t1), (SELECT count(*) FROM t2), (SELECT version FROM arm_schema WHERE arm='brazo');";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.Equal(0L, reader.GetInt64(1));
        Assert.Equal(3L, reader.GetInt64(2));
        Assert.Single(log);
    }

    [Fact]
    public void Si_paso_de_codigo_lanza_ni_sql_anterior_ni_version_quedan_escritos()
    {
        IReadOnlyList<ArmSchemaStep> steps =
        [
            ArmSchemaStep.Sql("CREATE TABLE fail_table (id INT);"),
            ArmSchemaStep.Code((conn, tx) => throw new InvalidOperationException("error simulado")),
        ];

        Assert.Throws<InvalidOperationException>(() => _db.ApplyArmSchema("falla", steps));

        using var conn = _db.Open();
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='fail_table';";
        var count = (long)check.ExecuteScalar()!;
        Assert.Equal(0L, count);

        using var checkVer = conn.CreateCommand();
        checkVer.CommandText = "SELECT count(*) FROM arm_schema WHERE arm='falla';";
        var verCount = (long)checkVer.ExecuteScalar()!;
        Assert.Equal(0L, verCount);
    }

    [Fact]
    public void Paso_de_codigo_ve_cambios_del_sql_anterior_en_la_misma_transaccion()
    {
        var readValue = 0L;
        IReadOnlyList<ArmSchemaStep> steps =
        [
            ArmSchemaStep.Sql("CREATE TABLE t_visible (val INT); INSERT INTO t_visible VALUES (42);"),
            ArmSchemaStep.Code((conn, tx) =>
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT val FROM t_visible;";
                readValue = (long)cmd.ExecuteScalar()!;
            }),
        ];

        _db.ApplyArmSchema("visible", steps);

        Assert.Equal(42L, readValue);
    }
}
