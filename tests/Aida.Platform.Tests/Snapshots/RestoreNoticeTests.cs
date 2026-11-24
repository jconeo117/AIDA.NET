using System.Security.Cryptography;
using Aida.Platform.Audit;
using Aida.Platform.Paths;
using Aida.Platform.Snapshots;
using Xunit;

namespace Aida.Platform.Tests.Snapshots;

public sealed class RestoreNoticeTests
{
    [Fact]
    public void Sin_aviso_no_se_escribe_linea_de_auditoria()
    {
        using var env = new NoticeEnv();
        env.Notice.SignIfPending(new Actor("u1", "Admin"), "127.0.0.1");
        Assert.Empty(env.Audit.Lines());
    }

    [Fact]
    public void Con_aviso_se_escribe_snapshot_restored_y_se_borra_el_archivo()
    {
        using var env = new NoticeEnv();
        var pending = new Dictionary<string, string>
        {
            ["aida_version"] = "1.0",
            ["created"] = "2026-11-24T00:00:00Z",
            ["files"] = "3",
            ["format"] = "1",
            ["previous"] = "antes-123",
            ["restored_at"] = "2026-11-24T01:00:00Z",
        };
        File.WriteAllBytes(env.Paths.RestorePendingFile, SnapshotManifest.Serialize(pending));

        env.Notice.SignIfPending(new Actor("u1", "Admin"), "127.0.0.1");

        Assert.False(File.Exists(env.Paths.RestorePendingFile));
        var lines = env.Audit.Lines();
        Assert.Single(lines);
        Assert.Contains("snapshot_restored", lines[0]);
        Assert.Contains("u1", lines[0]);
        Assert.Contains("antes-123", lines[0]);

        // Segunda llamada no hace nada
        env.Notice.SignIfPending(new Actor("u1", "Admin"), "127.0.0.1");
        Assert.Single(env.Audit.Lines());
    }

    [Fact]
    public void Con_json_roto_firma_con_claves_vacias()
    {
        using var env = new NoticeEnv();
        File.WriteAllText(env.Paths.RestorePendingFile, "{ json_invalido: 123");

        env.Notice.SignIfPending(new Actor("u2", "Operador"), "127.0.0.1");

        Assert.False(File.Exists(env.Paths.RestorePendingFile));
        var lines = env.Audit.Lines();
        Assert.Single(lines);
        Assert.Contains("snapshot_restored", lines[0]);
        Assert.Contains("u2", lines[0]);
    }

    private sealed class NoticeEnv : IDisposable
    {
        private readonly string _dir;
        public ServerPaths Paths { get; }
        public TestDatabase Db { get; }
        public AuditLog Audit { get; }
        public RestoreNotice Notice { get; }

        public NoticeEnv()
        {
            _dir = Path.Combine(Path.GetTempPath(), $"aida-notice-{RandomNumberGenerator.GetHexString(12)}");
            Directory.CreateDirectory(_dir);
            Paths = new ServerPaths(_dir, _dir);
            Db = new TestDatabase();
            Audit = new AuditLog(Db.Database, new ManualTime());
            Notice = new RestoreNotice(Paths, Audit);
        }

        public void Dispose()
        {
            Db.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }
    }
}
