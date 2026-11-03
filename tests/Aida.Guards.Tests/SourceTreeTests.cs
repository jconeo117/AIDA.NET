using System.Text;

namespace Aida.Guards.Tests;

public sealed class SourceTreeTests
{
    [Fact]
    public void Constructor_lanza_DirectoryNotFoundException_si_la_raiz_no_existe()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), "inexistente_" + Guid.NewGuid().ToString("N"));
        Assert.Throws<DirectoryNotFoundException>(() => new SourceTree(nonExistent));
    }

    [Fact]
    public void Excluye_carpetas_bin_y_obj_al_listar()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida_tree_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "Sub"));
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "bin", "Release"));
        Directory.CreateDirectory(Path.Combine(tempDir, "src", "obj", "Debug"));

        File.WriteAllText(Path.Combine(tempDir, "src", "Sub", "Valid.cs"), "content");
        File.WriteAllText(Path.Combine(tempDir, "src", "bin", "Release", "Ignored.cs"), "content");
        File.WriteAllText(Path.Combine(tempDir, "src", "obj", "Debug", "Ignored.cs"), "content");

        try
        {
            var tree = new SourceTree(tempDir);
            var files = tree.ListFiles("src", [".cs"]);

            Assert.Single(files);
            Assert.Equal("src/Sub/Valid.cs", files[0]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Rutas_devueltas_con_barra_inclinada_y_ordenadas_por_ordinal()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida_tree_sort_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempDir, "b"));
        Directory.CreateDirectory(Path.Combine(tempDir, "a"));

        File.WriteAllText(Path.Combine(tempDir, "b", "z.cs"), "content");
        File.WriteAllText(Path.Combine(tempDir, "a", "m.cs"), "content");
        File.WriteAllText(Path.Combine(tempDir, "a", "a.cs"), "content");

        try
        {
            var tree = new SourceTree(tempDir);
            var files = tree.ListFiles("", [".cs"]);

            Assert.Equal(3, files.Count);
            Assert.Equal("a/a.cs", files[0]);
            Assert.Equal("a/m.cs", files[1]);
            Assert.Equal("b/z.cs", files[2]);
            Assert.All(files, f => Assert.DoesNotContain('\\', f));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Carpeta_que_no_existe_devuelve_lista_vacia()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida_tree_empty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var tree = new SourceTree(tempDir);
            var files = tree.ListFiles("no_existe", [".cs"]);

            Assert.Empty(files);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadAllText_lee_contenido_en_formato_utf8()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida_tree_utf8_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var expectedText = "Texto con tildes: camión, pingüino y año.";
        File.WriteAllText(Path.Combine(tempDir, "archivo.txt"), expectedText, Encoding.UTF8);

        try
        {
            var tree = new SourceTree(tempDir);
            var content = tree.ReadAllText("archivo.txt");

            Assert.Equal(expectedText, content);
            Assert.Equal(Path.GetFullPath(tempDir), tree.Root);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
