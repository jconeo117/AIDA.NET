using Aida.Arms.Agenda.Actions;
using Aida.Arms.Agenda.Desk;
using Aida.Arms.Agenda.Storage;
using Aida.Engine.Actions;
using Aida.Platform.Keys;
using Aida.Platform.People;
using Aida.Platform.Sealing;
using Aida.Platform.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aida.Arms.Agenda.Tests.Actions;

public sealed class EditActionContactTests : IDisposable
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

    private readonly string _dir;
    private readonly PlatformDatabase _db;
    private readonly MachineKey _key;
    private readonly Sealer _sealer;
    private readonly PersonKeys _personKeys;
    private readonly PersonCatalog _catalog;
    private readonly DateOnly _today = new(2026, 11, 26);

    public EditActionContactTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "aida-editcontact-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        var dbPath = Path.Combine(_dir, "aida.db");
        _db = new PlatformDatabase(dbPath);

        var keyPath = Path.Combine(_dir, "machine.key");
        _key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
        _sealer = new Sealer(_key);
        _personKeys = PersonKeys.From(_key);
        _db.ApplyArmSchema(AgendaDeclarations.ArmId, AgendaSchema.Steps(_sealer, _personKeys));

        var repoRoot = FindRepoRoot();
        _catalog = PersonCatalog.Load(Path.Combine(repoRoot, "packs", "platform", "people.yaml"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir))
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch
            {
            }
        }
    }

    private AgendaWork Begin() => AgendaWork.Begin(_db, _sealer, _personKeys);

    [Theory]
    [InlineData("")]
    [InlineData("invalido")]
    [InlineData("999")]
    public void Contacto_inexistente_o_invalido_devuelve_unknown_client(string clientVal)
    {
        using var work = Begin();
        var args = new Dictionary<string, string>
        {
            ["client"] = clientVal,
            ["name"] = "Carlos",
            ["phone"] = "3001234567",
        };

        var result = EditAction.Contact(work.People, _catalog, _today, args);
        Assert.Equal(ToolStatus.Rejected, result.Status);
        Assert.Equal("unknown_client", result.Reason);
    }

    [Theory]
    [InlineData("", "3001234567")]
    [InlineData("   ", "3001234567")]
    [InlineData("Carlos", "")]
    [InlineData("Carlos", "   ")]
    public void Nombre_o_telefono_vacio_o_blancos_devuelve_missing_client(string name, string phone)
    {
        using var work = Begin();
        var c = work.People.Add("Carlos", "3001234567");
        work.Commit();

        using var testWork = Begin();
        var args = new Dictionary<string, string>
        {
            ["client"] = c.Id.ToString(),
            ["name"] = name,
            ["phone"] = phone,
        };

        var result = EditAction.Contact(testWork.People, _catalog, _today, args);
        Assert.Equal(ToolStatus.Rejected, result.Status);
        Assert.Equal("missing_client", result.Reason);
    }

    [Fact]
    public void Telefono_duplicado_de_otro_contacto_se_admite()
    {
        using var work = Begin();
        var c1 = work.People.Add("Carlos", "3001111111");
        var c2 = work.People.Add("Diana", "3002222222");
        work.Commit();

        using var testWork = Begin();
        var args = new Dictionary<string, string>
        {
            ["client"] = c2.Id.ToString(),
            ["name"] = "Diana",
            ["phone"] = "3001111111", // Mismo teléfono de Carlos (D5)
        };

        var result = EditAction.Contact(testWork.People, _catalog, _today, args);
        Assert.Equal(ToolStatus.Done, result.Status);
    }

    [Fact]
    public void Mismo_telefono_del_mismo_contacto_es_aceptado()
    {
        using var work = Begin();
        var c1 = work.People.Add("Carlos", "3001111111");
        work.Commit();

        using var testWork = Begin();
        var args = new Dictionary<string, string>
        {
            ["client"] = c1.Id.ToString(),
            ["name"] = "Carlos Andrés",
            ["phone"] = "3001111111",
        };

        var result = EditAction.Contact(testWork.People, _catalog, _today, args);
        Assert.Equal(ToolStatus.Done, result.Status);
        Assert.Equal(c1.Id.ToString(), result.Data["client"]);
    }

    [Fact]
    public void Edicion_guarda_campos_recortados_y_retorna_done()
    {
        using var work = Begin();
        var c = work.People.Add("Carlos", "3001111111");
        work.Commit();

        using var testWork = Begin();
        var args = new Dictionary<string, string>
        {
            ["client"] = c.Id.ToString(),
            ["name"] = "  Carlos Andrés  ",
            ["phone"] = "  3009999999  ",
        };

        var result = EditAction.Contact(testWork.People, _catalog, _today, args);
        testWork.Commit();

        Assert.Equal(ToolStatus.Done, result.Status);
        Assert.Equal(c.Id.ToString(), result.Data["client"]);

        using var queryWork = Begin();
        var updated = queryWork.People.Find(c.Id);
        Assert.NotNull(updated);
        Assert.Equal("Carlos Andrés", updated.Name);
        Assert.Equal("3009999999", updated.Phone);
    }

    [Fact]
    public void Cambio_de_telefono_mueve_la_huella_en_el_almacen()
    {
        using var work = Begin();
        var c = work.People.Add("Carlos", "3001111111");
        work.Commit();

        using var testWork = Begin();
        var args = new Dictionary<string, string>
        {
            ["client"] = c.Id.ToString(),
            ["name"] = "Carlos",
            ["phone"] = "3002222222",
        };

        var result = EditAction.Contact(testWork.People, _catalog, _today, args);
        testWork.Commit();
        Assert.Equal(ToolStatus.Done, result.Status);

        using var queryWork = Begin();
        Assert.Empty(queryWork.People.ByPhone("3001111111"));
        var nuevo = queryWork.People.ByPhone("3002222222");
        Assert.NotEmpty(nuevo);
        Assert.Equal(c.Id, nuevo[0].Id);
    }

    [Fact]
    public void Reglas_D6_rechazos_en_orden_y_borrado_con_vacios()
    {
        using var work = Begin();
        var c1 = work.People.Add("Carlos", "3001111111");
        var c2 = work.People.Add("Diana", "3002222222");
        work.People.UpdateDetails(c1.Id, new PersonDetails("CC", "12345678", new DateOnly(1990, 5, 10), "H"));
        work.Commit();

        using var testWork = Begin();
        // 1. incomplete_document (falta número)
        var r1 = EditAction.Contact(testWork.People, _catalog, _today, new Dictionary<string, string>
        {
            ["client"] = c2.Id.ToString(), ["name"] = "Diana", ["phone"] = "3002222222",
            ["document_type"] = "CC",
        });
        Assert.Equal("incomplete_document", r1.Reason);

        // 2. unknown_document_type
        var r2 = EditAction.Contact(testWork.People, _catalog, _today, new Dictionary<string, string>
        {
            ["client"] = c2.Id.ToString(), ["name"] = "Diana", ["phone"] = "3002222222",
            ["document_type"] = "XX", ["document_number"] = "87654321",
        });
        Assert.Equal("unknown_document_type", r2.Reason);

        // 3. bad_document (< 3 caracteres)
        var r3 = EditAction.Contact(testWork.People, _catalog, _today, new Dictionary<string, string>
        {
            ["client"] = c2.Id.ToString(), ["name"] = "Diana", ["phone"] = "3002222222",
            ["document_type"] = "CC", ["document_number"] = "12",
        });
        Assert.Equal("bad_document", r3.Reason);

        // 4. document_taken (mismo CC 12345678 de Carlos)
        var r4 = EditAction.Contact(testWork.People, _catalog, _today, new Dictionary<string, string>
        {
            ["client"] = c2.Id.ToString(), ["name"] = "Diana", ["phone"] = "3002222222",
            ["document_type"] = "CC", ["document_number"] = "12345678",
        });
        Assert.Equal("document_taken", r4.Reason);

        // 5. bad_birth_date (posterior a hoy)
        var r5 = EditAction.Contact(testWork.People, _catalog, _today, new Dictionary<string, string>
        {
            ["client"] = c2.Id.ToString(), ["name"] = "Diana", ["phone"] = "3002222222",
            ["birth_date"] = "2030-01-01",
        });
        Assert.Equal("bad_birth_date", r5.Reason);

        // 6. unknown_sex
        var r6 = EditAction.Contact(testWork.People, _catalog, _today, new Dictionary<string, string>
        {
            ["client"] = c2.Id.ToString(), ["name"] = "Diana", ["phone"] = "3002222222",
            ["sex"] = "Z",
        });
        Assert.Equal("unknown_sex", r6.Reason);

        // 7. Borrado con vacíos deja los detalles limpios
        var r7 = EditAction.Contact(testWork.People, _catalog, _today, new Dictionary<string, string>
        {
            ["client"] = c1.Id.ToString(), ["name"] = "Carlos", ["phone"] = "3001111111",
            ["document_type"] = "", ["document_number"] = "", ["birth_date"] = "", ["sex"] = "",
        });
        Assert.Equal(ToolStatus.Done, r7.Status);
        testWork.Commit();

        using var queryWork = Begin();
        var cleared = queryWork.People.Find(c1.Id);
        Assert.NotNull(cleared);
        Assert.Equal(PersonDetails.None, cleared.Details);
    }

    [Fact]
    public void Busqueda_por_digitos_del_documento_D7()
    {
        using var work = Begin();
        var p = work.People.Add("Niño Ejemplo", "3005559999");
        work.People.UpdateDetails(p.Id, new PersonDetails("TI", "1.098.765.432", new DateOnly(2015, 6, 1), "H"));
        work.Commit();

        using var queryWork = Begin();
        // Búsqueda solo con dígitos casa con el documento formateado
        var found = ClientSearch.Find(queryWork.People, "098765");
        var single = Assert.Single(found);
        Assert.Equal(p.Id, single.Id);
        Assert.Equal("Niño Ejemplo", single.Name);
    }
}
