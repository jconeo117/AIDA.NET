using Aida.Platform.Audit;
using Aida.Platform.Ledger;
using Xunit;

namespace Aida.Platform.Tests.Audit;

public sealed class AuditLogTests
{
    [Fact]
    public void La_linea_lleva_hora_local_clase_autor_puesto_y_datos()
    {
        using var db = new TestDatabase();
        var time = new ManualTime();
        var log = new AuditLog(db.Database, time);

        var digest = log.Record(
            AuditKind.SessionOpened,
            new Actor("u1", "Ana Pérez"),
            "192.168.1.20",
            new Dictionary<string, string>());

        var lines = log.Lines();
        Assert.Single(lines);

        var line = lines[0];
        var expectedLine =
            "{\"actor\": \"u1\", \"actor_name\": \"Ana Pérez\", \"at\": \"2026-11-15T10:20:30-05:00\", \"data\": \"{}\", " +
            $"\"digest\": \"{digest}\", \"kind\": \"session_opened\", \"prev\": \"{ChainedLog.Genesis}\", \"workstation\": \"192.168.1.20\"}}";

        Assert.Equal(expectedLine, line);

        var problems = log.Verify();
        Assert.Empty(problems);
    }

    [Fact]
    public void Todas_las_clases_tienen_su_nombre_de_la_01()
    {
        var expectedWireNames = new Dictionary<AuditKind, string>
        {
            [AuditKind.SessionOpened] = "session_opened",
            [AuditKind.SessionClosed] = "session_closed",
            [AuditKind.Locked] = "locked",
            [AuditKind.Unlocked] = "unlocked",
            [AuditKind.UserCreated] = "user_created",
            [AuditKind.UserDisabled] = "user_disabled",
            [AuditKind.PasswordReset] = "password_reset",
        };

        using var db = new TestDatabase();
        var time = new ManualTime();
        var log = new AuditLog(db.Database, time);
        var actor = new Actor("u1", "Ana");

        foreach (var kind in AuditKind.Platform)
        {
            Assert.True(expectedWireNames.TryGetValue(kind, out var expectedName), $"Falta mapear AuditKind.{kind}");
            log.Record(kind, actor, "127.0.0.1", new Dictionary<string, string>());
        }

        var lines = log.Lines();
        Assert.Equal(expectedWireNames.Count, lines.Count);

        var index = 0;
        foreach (var kind in AuditKind.Platform)
        {
            var wire = expectedWireNames[kind];
            Assert.Contains($"\"kind\": \"{wire}\"", lines[index]);
            index++;
        }
    }

    [Fact]
    public void Una_clase_de_brazo_declarada_se_escribe()
    {
        var armKind = AuditKind.Declare("agenda_changed");
        using var db = new TestDatabase();
        var time = new ManualTime();
        var log = new AuditLog(db.Database, time, [armKind]);
        var actor = new Actor("u1", "Ana");

        var digest = log.Record(armKind, actor, "127.0.0.1", new Dictionary<string, string> { ["action"] = "agendar" });

        var lines = log.Lines();
        Assert.Single(lines);
        Assert.Contains("\"kind\": \"agenda_changed\"", lines[0]);
        Assert.Empty(log.Verify());
    }

    [Fact]
    public void Una_clase_no_declarada_lanza_y_no_escribe()
    {
        var undeclaredKind = AuditKind.Declare("custom_unknown");
        using var db = new TestDatabase();
        var time = new ManualTime();
        var log = new AuditLog(db.Database, time);
        var actor = new Actor("u1", "Ana");

        var ex = Assert.Throws<ArgumentException>(() =>
            log.Record(undeclaredKind, actor, "127.0.0.1", new Dictionary<string, string>()));
        Assert.Contains("clase de hecho de auditoría desconocida", ex.Message);
        Assert.Empty(log.Lines());
    }

    [Fact]
    public void En_la_transaccion_de_quien_llama_sin_commit_no_queda()
    {
        var armKind = AuditKind.Declare("agenda_changed");
        using var db = new TestDatabase();
        var time = new ManualTime();
        var log = new AuditLog(db.Database, time, [armKind]);
        var actor = new Actor("u1", "Ana");

        using (var conn = db.Database.Open())
        using (var tx = conn.BeginTransaction())
        {
            log.Record(conn, tx, armKind, actor, "127.0.0.1", new Dictionary<string, string>());
            // No hacemos tx.Commit()
        }

        Assert.Empty(log.Lines());
    }

    [Fact]
    public void Un_hecho_sin_autor_no_se_escribe()
    {
        using var db = new TestDatabase();
        var time = new ManualTime();
        var log = new AuditLog(db.Database, time);

        Assert.Throws<ArgumentNullException>(() =>
            log.Record(AuditKind.SessionOpened, null!, "127.0.0.1", new Dictionary<string, string>()));

        Assert.Empty(log.Lines());
    }
}
