using Aida.Platform.Packs;
using Xunit;

namespace Aida.Platform.Tests.Packs;

public sealed class PlatformYamlTests
{
    [Fact]
    public void Lee_un_mapa()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "clave: valor\notro: 123\n");
            var root = PlatformYaml.ReadRoot(tempFile);
            Assert.NotNull(root);
            Assert.Equal("valor", PlatformYaml.Text(PlatformYaml.Child(root, "clave")));
            Assert.Equal("123", PlatformYaml.Text(PlatformYaml.Child(root, "otro")));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void No_es_un_mapa_o_no_es_yaml_falla_con_la_ruta()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // 1. Archivo que es una lista y no un mapa
            File.WriteAllText(tempFile, "- item1\n- item2\n");
            var ex1 = Assert.Throws<InvalidDataException>(() => PlatformYaml.ReadRoot(tempFile));
            Assert.StartsWith(tempFile, ex1.Message);
            Assert.Contains("tiene que ser un único mapa", ex1.Message);

            // 2. Archivo con YAML inválido
            File.WriteAllText(tempFile, "esto: no es : [yaml valido\n");
            var ex2 = Assert.Throws<InvalidDataException>(() => PlatformYaml.ReadRoot(tempFile));
            Assert.StartsWith(tempFile, ex2.Message);
            Assert.Contains("no es YAML válido", ex2.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
