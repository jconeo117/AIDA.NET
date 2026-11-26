using Aida.Platform.People;
using Xunit;

namespace Aida.Platform.Tests.People;

public sealed class PersonCatalogTests
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
    public void Carga_el_pack_real_del_repositorio()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "packs", "platform", "people.yaml");

        var catalog = PersonCatalog.Load(path);
        Assert.NotNull(catalog);
        Assert.NotEmpty(catalog.DocumentTypes);
        Assert.NotEmpty(catalog.Sexes);

        Assert.True(catalog.HasDocumentType("CC"));
        Assert.True(catalog.HasDocumentType("TI"));
        Assert.True(catalog.HasSex("M"));
        Assert.True(catalog.HasSex("H"));
        Assert.False(catalog.HasDocumentType("INEXISTENTE"));
        Assert.False(catalog.HasSex("X"));
    }

    [Fact]
    public void Falta_seccion_people_lanza_con_ruta()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "otra_cosa:\n  a: 1\n");
            var ex = Assert.Throws<InvalidDataException>(() => PersonCatalog.Load(tempFile));
            Assert.Contains(tempFile, ex.Message);
            Assert.Contains("falta la sección 'people'", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("document_types", "people:\n  sexes:\n    - {id: 'M', label: 'Mujer'}\n")]
    [InlineData("sexes", "people:\n  document_types:\n    - {id: 'CC', label: 'Cédula'}\n")]
    public void Falta_una_lista_lanza_con_ruta(string lista, string yaml)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, yaml);
            var ex = Assert.Throws<InvalidDataException>(() => PersonCatalog.Load(tempFile));
            Assert.Contains(tempFile, ex.Message);
            Assert.Contains($"falta la lista '{lista}'", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Id_invalido_o_vacio_lanza_con_ruta()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                people:
                  document_types:
                    - {id: "12345", label: "Inválido"}
                  sexes:
                    - {id: "M", label: "Mujer"}
                """);
            var ex = Assert.Throws<InvalidDataException>(() => PersonCatalog.Load(tempFile));
            Assert.Contains(tempFile, ex.Message);
            Assert.Contains("no es válido", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Id_repetido_lanza_con_ruta()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                people:
                  document_types:
                    - {id: "CC", label: "Cédula"}
                    - {id: "CC", label: "Cédula repetida"}
                  sexes:
                    - {id: "M", label: "Mujer"}
                """);
            var ex = Assert.Throws<InvalidDataException>(() => PersonCatalog.Load(tempFile));
            Assert.Contains(tempFile, ex.Message);
            Assert.Contains("está repetido", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Etiqueta_vacia_lanza_con_ruta()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """
                people:
                  document_types:
                    - {id: "CC", label: ""}
                  sexes:
                    - {id: "M", label: "Mujer"}
                """);
            var ex = Assert.Throws<InvalidDataException>(() => PersonCatalog.Load(tempFile));
            Assert.Contains(tempFile, ex.Message);
            Assert.Contains("no tiene etiqueta", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
