using Aida.Platform.Audit;
using Aida.Platform.Identity;
using Aida.Platform.Roles;
using Xunit;

namespace Aida.Platform.Tests.Identity;

public sealed class UserStoreTests
{
    private static RoleCatalog DefaultCatalog => new([
        new RoleSpec("administracion", "Administración", new HashSet<string> { PlatformPermission.ManageUsers }),
        new RoleSpec("recepcion", "Recepción", new HashSet<string>()),
        new RoleSpec("profesional", "Profesional", new HashSet<string>()),
    ]);

    private static (TestDatabase Db, ManualTime Time, UserStore Store, AuditLog Audit) CreateContext()
    {
        var db = new TestDatabase();
        var time = new ManualTime();
        var audit = new AuditLog(db.Database, time);
        var hasher = new PasswordHasher(10);
        var store = new UserStore(db.Database, audit, DefaultCatalog, hasher, time);
        return (db, time, store, audit);
    }

    [Fact]
    public void El_primer_administrador_firma_su_propia_creacion()
    {
        using var ctx = CreateContext().Db;
        var (db, time, store, audit) = CreateContext();

        Assert.False(store.HasUsers());
        var admin = store.CreateFirstAdministrator("admin", "Admin Principal", "contrasena123", "127.0.0.1");

        Assert.True(store.HasUsers());
        Assert.Equal("admin", admin.UserName);
        Assert.Equal("administracion", admin.RoleId);

        var lines = audit.Lines();
        Assert.Single(lines);
        Assert.Contains($"\"actor\": \"{admin.Id}\"", lines[0]);
        Assert.Contains("\"actor_name\": \"Admin Principal\"", lines[0]);
        Assert.Contains("\"kind\": \"user_created\"", lines[0]);
    }

    [Fact]
    public void El_primer_administrador_solo_una_vez()
    {
        var (db, _, store, _) = CreateContext();
        using (db)
        {
            store.CreateFirstAdministrator("admin", "Admin Principal", "contrasena123", "127.0.0.1");

            var ex = Assert.Throws<IdentityException>(() =>
                store.CreateFirstAdministrator("admin2", "Segundo Admin", "contrasena123", "127.0.0.1"));
            Assert.Equal(IdentityError.AlreadyInitialized, ex.Error);
        }
    }

    [Fact]
    public void El_usuario_se_guarda_en_minusculas_y_el_nombre_sin_espacios_repetidos()
    {
        var (db, _, store, _) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("  AdMin.User  ", "  Ana   María    Pérez  ", "contrasena123", "127.0.0.1");

            Assert.Equal("admin.user", admin.UserName);
            Assert.Equal("Ana María Pérez", admin.DisplayName);

            var verified = store.Verify("ADMIN.USER", "contrasena123");
            Assert.Equal("admin.user", verified.UserName);
            Assert.Equal("Ana María Pérez", verified.DisplayName);
        }
    }

    [Fact]
    public void Las_formas_que_no_valen()
    {
        var (db, _, store, _) = CreateContext();
        using (db)
        {
            // usuario vacío
            var ex1 = Assert.Throws<IdentityException>(() =>
                store.CreateFirstAdministrator("", "Nombre Valido", "contrasena123", "127.0.0.1"));
            Assert.Equal(IdentityError.Missing, ex1.Error);

            // usuario de 2 caracteres
            var ex2 = Assert.Throws<IdentityException>(() =>
                store.CreateFirstAdministrator("ab", "Nombre Valido", "contrasena123", "127.0.0.1"));
            Assert.Equal(IdentityError.UserNameFormat, ex2.Error);

            // usuario de 33 caracteres
            var ex3 = Assert.Throws<IdentityException>(() =>
                store.CreateFirstAdministrator(new string('a', 33), "Nombre Valido", "contrasena123", "127.0.0.1"));
            Assert.Equal(IdentityError.UserNameFormat, ex3.Error);

            // usuario con tilde
            var ex4 = Assert.Throws<IdentityException>(() =>
                store.CreateFirstAdministrator("pérez", "Nombre Valido", "contrasena123", "127.0.0.1"));
            Assert.Equal(IdentityError.UserNameFormat, ex4.Error);

            // contraseña de 9 caracteres
            var ex5 = Assert.Throws<IdentityException>(() =>
                store.CreateFirstAdministrator("admin", "Nombre Valido", "123456789", "127.0.0.1"));
            Assert.Equal(IdentityError.PasswordFormat, ex5.Error);

            // contraseña de 129 caracteres
            var ex6 = Assert.Throws<IdentityException>(() =>
                store.CreateFirstAdministrator("admin", "Nombre Valido", new string('a', 129), "127.0.0.1"));
            Assert.Equal(IdentityError.PasswordFormat, ex6.Error);

            // nombre vacío
            var ex7 = Assert.Throws<IdentityException>(() =>
                store.CreateFirstAdministrator("admin", "   ", "contrasena123", "127.0.0.1"));
            Assert.Equal(IdentityError.Missing, ex7.Error);

            // nombre de 81 caracteres
            var ex8 = Assert.Throws<IdentityException>(() =>
                store.CreateFirstAdministrator("admin", new string('a', 81), "contrasena123", "127.0.0.1"));
            Assert.Equal(IdentityError.Missing, ex8.Error);
        }
    }

    [Fact]
    public void Un_usuario_repetido_aunque_cambien_las_mayusculas()
    {
        var (db, _, store, _) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");

            store.Create(admin.Id, "127.0.0.1", "usuario1", "Usuario Uno", "recepcion", "contrasena123");

            var ex = Assert.Throws<IdentityException>(() =>
                store.Create(admin.Id, "127.0.0.1", "USUARIO1", "Usuario Uno Bis", "recepcion", "contrasena123"));
            Assert.Equal(IdentityError.Duplicate, ex.Error);
        }
    }

    [Fact]
    public void Un_rol_no_declarado_no_se_asigna()
    {
        var (db, _, store, _) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");

            var ex = Assert.Throws<IdentityException>(() =>
                store.Create(admin.Id, "127.0.0.1", "usuario1", "Usuario Uno", "rol_inexistente", "contrasena123"));
            Assert.Equal(IdentityError.RoleUnknown, ex.Error);
        }
    }

    [Fact]
    public void La_espera_llega_en_el_quinto_fallo_y_dura_treinta_segundos()
    {
        var (db, time, store, _) = CreateContext();
        using (db)
        {
            store.CreateFirstAdministrator("admin", "Admin", "contrasena-buena", "127.0.0.1");

            // 4 fallos dan BadCredentials y AttemptsLeft vale 1 al 4.º
            for (var intento = 1; intento <= 4; intento++)
            {
                var ex = Assert.Throws<IdentityException>(() => store.Verify("admin", "contrasena-mala"));
                Assert.Equal(IdentityError.BadCredentials, ex.Error);
                Assert.Equal(5 - intento, store.AttemptsLeft("admin"));
            }

            // El 5.º da Wait
            var ex5 = Assert.Throws<IdentityException>(() => store.Verify("admin", "contrasena-mala"));
            Assert.Equal(IdentityError.Wait, ex5.Error);

            // La contraseña buena también da Wait
            var exWait = Assert.Throws<IdentityException>(() => store.Verify("admin", "contrasena-buena"));
            Assert.Equal(IdentityError.Wait, exWait.Error);

            // Tras pasar 30 s, la contraseña buena entra
            time.Advance(TimeSpan.FromSeconds(30));
            var user = store.Verify("admin", "contrasena-buena");
            Assert.NotNull(user);
        }
    }

    [Fact]
    public void Inexistente_desactivado_y_contrasena_mala_dan_el_mismo_error()
    {
        var (db, _, store, _) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");
            var user = store.Create(admin.Id, "127.0.0.1", "inactivo", "Usuario Inactivo", "recepcion", "contrasena-correcta");
            store.Disable(admin.Id, "127.0.0.1", user.Id);

            // Usuario inexistente
            var ex1 = Assert.Throws<IdentityException>(() => store.Verify("no-existe", "cualquiera123"));
            Assert.Equal(IdentityError.BadCredentials, ex1.Error);

            // Contraseña incorrecta para usuario existente
            var ex2 = Assert.Throws<IdentityException>(() => store.Verify("admin", "contrasena-mala"));
            Assert.Equal(IdentityError.BadCredentials, ex2.Error);

            // Usuario desactivado con contraseña correcta
            var ex3 = Assert.Throws<IdentityException>(() => store.Verify("inactivo", "contrasena-correcta"));
            Assert.Equal(IdentityError.BadCredentials, ex3.Error);
        }
    }

    [Fact]
    public void Un_rol_sin_permiso_no_crea_no_desactiva_y_no_restablece()
    {
        var (db, _, store, audit) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");
            var recep = store.Create(admin.Id, "127.0.0.1", "recep", "Recepcionista", "recepcion", "contrasena123");

            var linesBefore = audit.Lines().Count;

            var exCreate = Assert.Throws<IdentityException>(() =>
                store.Create(recep.Id, "127.0.0.1", "nuevo", "Nuevo", "profesional", "contrasena123"));
            Assert.Equal(IdentityError.Forbidden, exCreate.Error);

            var exDisable = Assert.Throws<IdentityException>(() =>
                store.Disable(recep.Id, "127.0.0.1", admin.Id));
            Assert.Equal(IdentityError.Forbidden, exDisable.Error);

            var exReset = Assert.Throws<IdentityException>(() =>
                store.ResetPassword(recep.Id, "127.0.0.1", admin.Id, "nueva-clave123"));
            Assert.Equal(IdentityError.Forbidden, exReset.Error);

            Assert.Equal(linesBefore, audit.Lines().Count);
        }
    }

    [Fact]
    public void No_se_desactiva_al_ultimo_administrador()
    {
        var (db, _, store, _) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");

            var ex = Assert.Throws<IdentityException>(() => store.Disable(admin.Id, "127.0.0.1", admin.Id));
            Assert.Equal(IdentityError.LastAdministrator, ex.Error);

            var admin2 = store.Create(admin.Id, "127.0.0.1", "admin2", "Segundo Admin", "administracion", "contrasena123");
            // Ahora sí se puede desactivar al primero porque queda admin2
            store.Disable(admin2.Id, "127.0.0.1", admin.Id);

            // Pero no se puede desactivar al segundo porque es el último activo
            var ex2 = Assert.Throws<IdentityException>(() => store.Disable(admin2.Id, "127.0.0.1", admin2.Id));
            Assert.Equal(IdentityError.LastAdministrator, ex2.Error);
        }
    }

    [Fact]
    public void Restablecer_cambia_la_contrasena_y_queda_en_la_auditoria()
    {
        var (db, _, store, audit) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");
            var user = store.Create(admin.Id, "127.0.0.1", "usuario", "Usuario Común", "recepcion", "clave-inicial1");

            store.ResetPassword(admin.Id, "127.0.0.1", user.Id, "clave-nueva123");

            // La vieja ya no entra
            Assert.Throws<IdentityException>(() => store.Verify("usuario", "clave-inicial1"));

            // La nueva entra
            var verified = store.Verify("usuario", "clave-nueva123");
            Assert.NotNull(verified);

            var lastLine = audit.Lines().Last();
            Assert.Contains("\"kind\": \"password_reset\"", lastLine);
            Assert.Contains($"\"actor\": \"{admin.Id}\"", lastLine);
            Assert.Contains($"\\\"user\\\": \\\"{user.Id}\\\"", lastLine);
        }
    }

    [Fact]
    public void Crear_y_su_linea_van_juntos()
    {
        var (db, _, store, audit) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Admin", "contrasena123", "127.0.0.1");
            store.Create(admin.Id, "127.0.0.1", "ana", "Ana Gómez", "recepcion", "contrasena123");
            var lineCount = audit.Lines().Count;
            Assert.Equal(2, lineCount);

            // Repetido falla por UNIQUE en users
            Assert.Throws<IdentityException>(() =>
                store.Create(admin.Id, "127.0.0.1", "ana", "Otra Ana", "recepcion", "contrasena123"));

            // La auditoría no crece
            Assert.Equal(lineCount, audit.Lines().Count);
        }
    }

    [Fact]
    public void La_lista_va_por_nombre_y_luego_por_id()
    {
        var (db, _, store, _) = CreateContext();
        using (db)
        {
            var admin = store.CreateFirstAdministrator("admin", "Carlos", "contrasena123", "127.0.0.1");
            store.Create(admin.Id, "127.0.0.1", "user.b2", "Beatriz", "recepcion", "contrasena123");
            store.Create(admin.Id, "127.0.0.1", "user.b1", "Beatriz", "recepcion", "contrasena123");
            store.Create(admin.Id, "127.0.0.1", "user.a1", "Ana", "recepcion", "contrasena123");

            var list = store.List();
            Assert.Equal(4, list.Count);
            Assert.Equal("Ana", list[0].DisplayName);
            Assert.Equal("Beatriz", list[1].DisplayName);
            Assert.Equal("Beatriz", list[2].DisplayName);
            Assert.True(list[1].Id < list[2].Id);
            Assert.Equal("Carlos", list[3].DisplayName);
        }
    }
}
