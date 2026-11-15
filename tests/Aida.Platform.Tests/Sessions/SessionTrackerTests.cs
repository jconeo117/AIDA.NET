using Aida.Platform.Audit;
using Aida.Platform.Identity;
using Aida.Platform.Roles;
using Aida.Platform.Sessions;
using Xunit;

namespace Aida.Platform.Tests.Sessions;

public sealed class SessionTrackerTests
{
    private static (TestDatabase Db, ManualTime Time, UserStore Store, AuditLog Audit, SessionTracker Tracker) CreateContext()
    {
        var db = new TestDatabase();
        var time = new ManualTime();
        var audit = new AuditLog(db.Database, time);
        var catalog = new RoleCatalog([
            new RoleSpec("administracion", "Administración", new HashSet<string> { PlatformPermission.ManageUsers }),
            new RoleSpec("recepcion", "Recepción", new HashSet<string>()),
        ]);
        var hasher = new PasswordHasher(10);
        var store = new UserStore(db.Database, audit, catalog, hasher, time);
        var tracker = new SessionTracker(store, audit, time, TimeSpan.FromMinutes(10));
        return (db, time, store, audit, tracker);
    }

    [Fact]
    public void Abrir_registra_la_entrada()
    {
        var (db, _, store, audit, tracker) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");
            var sessionId = tracker.Open(admin, "192.168.1.10");

            Assert.False(string.IsNullOrWhiteSpace(sessionId));
            var view = tracker.Check(sessionId);
            Assert.Equal(SessionStatus.Active, view.Status);
            Assert.NotNull(view.User);
            Assert.Equal(admin.Id, view.User.Id);

            var lastLine = audit.Lines().Last();
            Assert.Contains("\"kind\": \"session_opened\"", lastLine);
            Assert.Contains("\"workstation\": \"192.168.1.10\"", lastLine);
        }
    }

    [Fact]
    public void Se_bloquea_justo_al_cumplir_el_tiempo_sin_uso()
    {
        var (db, time, store, audit, tracker) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");
            var sessionId = tracker.Open(admin, "127.0.0.1");

            // A los 9m 59s sigue activa
            time.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
            Assert.Equal(SessionStatus.Active, tracker.Check(sessionId).Status);

            // Justo al cumplir los 10m se bloquea
            time.Advance(TimeSpan.FromSeconds(1));
            var view = tracker.Check(sessionId);
            Assert.Equal(SessionStatus.Locked, view.Status);

            var lastLine = audit.Lines().Last();
            Assert.Contains("\"kind\": \"locked\"", lastLine);
        }
    }

    [Fact]
    public void La_actividad_aplaza_el_bloqueo_y_no_desbloquea()
    {
        var (db, time, store, _, tracker) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");
            var sessionId = tracker.Open(admin, "127.0.0.1");

            // Pasan 6 minutos y hay actividad
            time.Advance(TimeSpan.FromMinutes(6));
            tracker.Touch(sessionId);

            // Pasan otros 6 minutos (total 12 minutos desde apertura, pero solo 6 desde actividad)
            time.Advance(TimeSpan.FromMinutes(6));
            Assert.Equal(SessionStatus.Active, tracker.Check(sessionId).Status);

            // Ahora avanzamos 4 minutos más (10 minutos desde touch) -> se bloquea
            time.Advance(TimeSpan.FromMinutes(4));
            Assert.Equal(SessionStatus.Locked, tracker.Check(sessionId).Status);

            // Touch sobre sesión bloqueada no desbloquea
            tracker.Touch(sessionId);
            Assert.Equal(SessionStatus.Locked, tracker.Check(sessionId).Status);
        }
    }

    [Fact]
    public void Desbloquear_pide_la_contrasena_de_la_misma_persona()
    {
        var (db, time, store, audit, tracker) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");
            var sessionId = tracker.Open(admin, "127.0.0.1");

            time.Advance(TimeSpan.FromMinutes(10));
            Assert.Equal(SessionStatus.Locked, tracker.Check(sessionId).Status);

            // Contraseña incorrecta falla
            var ex = Assert.Throws<IdentityException>(() => tracker.Unlock(sessionId, "contrasena-incorrecta"));
            Assert.Equal(IdentityError.BadCredentials, ex.Error);
            Assert.Equal(SessionStatus.Locked, tracker.Check(sessionId).Status);

            // Contraseña correcta desbloquea
            var user = tracker.Unlock(sessionId, "contrasena123");
            Assert.Equal(admin.Id, user.Id);
            Assert.Equal(SessionStatus.Active, tracker.Check(sessionId).Status);

            var lastLine = audit.Lines().Last();
            Assert.Contains("\"kind\": \"unlocked\"", lastLine);
        }
    }

    [Fact]
    public void Desbloquear_falla_con_la_misma_espera_que_entrar()
    {
        var (db, time, store, _, tracker) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");
            var sessionId = tracker.Open(admin, "127.0.0.1");

            time.Advance(TimeSpan.FromMinutes(10));
            Assert.Equal(SessionStatus.Locked, tracker.Check(sessionId).Status);

            for (var i = 1; i <= 4; i++)
            {
                var ex = Assert.Throws<IdentityException>(() => tracker.Unlock(sessionId, "mala"));
                Assert.Equal(IdentityError.BadCredentials, ex.Error);
            }

            var ex5 = Assert.Throws<IdentityException>(() => tracker.Unlock(sessionId, "mala"));
            Assert.Equal(IdentityError.Wait, ex5.Error);

            // Durante la espera la correcta da Wait
            var exWait = Assert.Throws<IdentityException>(() => tracker.Unlock(sessionId, "contrasena123"));
            Assert.Equal(IdentityError.Wait, exWait.Error);

            time.Advance(TimeSpan.FromSeconds(30));
            var user = tracker.Unlock(sessionId, "contrasena123");
            Assert.NotNull(user);
        }
    }

    [Fact]
    public void Cerrar_registra_la_salida_y_cerrar_dos_veces_no_hace_nada()
    {
        var (db, _, store, audit, tracker) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");
            var sessionId = tracker.Open(admin, "127.0.0.1");

            tracker.Close(sessionId);

            var view = tracker.Check(sessionId);
            Assert.Equal(SessionStatus.None, view.Status);
            Assert.Null(view.User);

            var lastLine = audit.Lines().Last();
            Assert.Contains("\"kind\": \"session_closed\"", lastLine);

            var linesCount = audit.Lines().Count;
            // Cerrar por segunda vez no hace nada
            tracker.Close(sessionId);
            Assert.Equal(linesCount, audit.Lines().Count);
        }
    }

    [Fact]
    public void Un_usuario_desactivado_pierde_su_sesion()
    {
        var (db, _, store, _, tracker) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");
            var user = store.Create(admin.Id, "127.0.0.1", "operador", "Operador", "recepcion", "contrasena123");
            var sessionId = tracker.Open(user, "127.0.0.1");

            Assert.Equal(SessionStatus.Active, tracker.Check(sessionId).Status);

            store.Disable(admin.Id, "127.0.0.1", user.Id);

            var view = tracker.Check(sessionId);
            Assert.Equal(SessionStatus.None, view.Status);
            Assert.Null(view.User);
        }
    }

    [Fact]
    public void La_secuencia_de_hechos()
    {
        var (db, time, store, audit, tracker) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");
            var sessionId = tracker.Open(admin, "127.0.0.1");

            time.Advance(TimeSpan.FromMinutes(10));
            tracker.Check(sessionId);

            tracker.Unlock(sessionId, "contrasena123");

            tracker.Close(sessionId);

            var lines = audit.Lines();
            Assert.Equal(5, lines.Count);
            Assert.Contains("\"kind\": \"user_created\"", lines[0]);
            Assert.Contains("\"kind\": \"session_opened\"", lines[1]);
            Assert.Contains("\"kind\": \"locked\"", lines[2]);
            Assert.Contains("\"kind\": \"unlocked\"", lines[3]);
            Assert.Contains("\"kind\": \"session_closed\"", lines[4]);

            Assert.Empty(audit.Verify());
        }
    }
}
