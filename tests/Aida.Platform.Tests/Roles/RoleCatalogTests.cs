using Aida.Platform.Roles;
using Xunit;

namespace Aida.Platform.Tests.Roles;

public sealed class RoleCatalogTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    [Fact]
    public void Lee_roles_yaml_en_su_orden()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                roles:
                  - id: primero
                    label: "Primer Rol"
                    permissions: [users.manage]
                  - id: segundo
                    label: "Segundo Rol"
                  - id: tercero
                    label: "Tercer Rol"
                """);

            var catalog = RoleCatalog.Load(tempFile);
            Assert.Equal(3, catalog.Roles.Count);
            Assert.Equal("primero", catalog.Roles[0].Id);
            Assert.Equal("segundo", catalog.Roles[1].Id);
            Assert.Equal("tercero", catalog.Roles[2].Id);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void El_primer_administrador_es_el_primer_rol_que_gestiona_usuarios()
    {
        var catalog = new RoleCatalog([
            new RoleSpec("recepcion", "Recepción", new HashSet<string>()),
            new RoleSpec("admin_supremo", "Admin Supremo", new HashSet<string> { PlatformPermission.ManageUsers }),
            new RoleSpec("admin_secundario", "Admin Secundario", new HashSet<string> { PlatformPermission.ManageUsers }),
        ]);

        Assert.Equal("admin_supremo", catalog.FirstAdministratorRole.Id);
    }

    [Fact]
    public void Un_rol_no_declarado_no_tiene_permisos()
    {
        var catalog = new RoleCatalog([
            new RoleSpec("admin", "Admin", new HashSet<string> { PlatformPermission.ManageUsers }),
        ]);

        Assert.False(catalog.Allows("fantasma", PlatformPermission.ManageUsers));
        Assert.False(catalog.Contains("fantasma"));
    }

    [Fact]
    public void Los_catalogos_que_no_valen()
    {
        // vacío
        var exEmpty = Assert.Throws<InvalidDataException>(() => new RoleCatalog([]));
        Assert.Contains("no declara ningún rol", exEmpty.Message);

        // id repetido
        var exDup = Assert.Throws<InvalidDataException>(() => new RoleCatalog([
            new RoleSpec("admin", "Admin 1", new HashSet<string> { PlatformPermission.ManageUsers }),
            new RoleSpec("admin", "Admin 2", new HashSet<string>()),
        ]));
        Assert.Contains("está repetido", exDup.Message);

        // id con mayúscula
        var exUpper = Assert.Throws<InvalidDataException>(() => new RoleCatalog([
            new RoleSpec("Admin", "Admin", new HashSet<string> { PlatformPermission.ManageUsers }),
        ]));
        Assert.Contains("no es un identificador válido", exUpper.Message);

        // sin etiqueta
        var exNoLabel = Assert.Throws<InvalidDataException>(() => new RoleCatalog([
            new RoleSpec("admin", "   ", new HashSet<string> { PlatformPermission.ManageUsers }),
        ]));
        Assert.Contains("no tiene etiqueta", exNoLabel.Message);

        // permiso desconocido
        var exUnknownPerm = Assert.Throws<InvalidDataException>(() => new RoleCatalog([
            new RoleSpec("admin", "Admin", new HashSet<string> { PlatformPermission.ManageUsers, "permiso.raro" }),
        ]));
        Assert.Contains("permiso desconocido", exUnknownPerm.Message);

        // ningún rol con users.manage
        var exNoManager = Assert.Throws<InvalidDataException>(() => new RoleCatalog([
            new RoleSpec("recepcion", "Recepción", new HashSet<string>()),
        ]));
        Assert.Contains("ningún rol tiene", exNoManager.Message);

        // falta roles en YAML
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "otro_campo: valor\n");
            var exNoRoles = Assert.Throws<InvalidDataException>(() => RoleCatalog.Load(tempFile));
            Assert.Contains("falta la lista 'roles'", exNoRoles.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Lee_el_roles_yaml_del_repositorio()
    {
        var path = Path.Combine(FindRepoRoot(), "packs", "platform", "roles.yaml");
        var catalog = RoleCatalog.Load(path);

        Assert.NotEmpty(catalog.Roles);
        Assert.Equal("administracion", catalog.FirstAdministratorRole.Id);
        Assert.True(catalog.Allows("administracion", PlatformPermission.ManageUsers));
        Assert.False(catalog.Allows("recepcion", PlatformPermission.ManageUsers));
    }
}
