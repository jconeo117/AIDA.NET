using Aida.Platform.Paths;
using Xunit;

namespace Aida.Platform.Tests.Paths;

public sealed class ServerPathsTests
{
    [Theory]
    [InlineData("", "/tmp/data")]
    [InlineData("   ", "/tmp/data")]
    [InlineData("relativo/packs", "/tmp/data")]
    [InlineData("/tmp/packs", "")]
    [InlineData("/tmp/packs", "relativo/data")]
    public void Las_rutas_tienen_que_ser_absolutas(string packs, string data)
    {
        var ex = Assert.Throws<ArgumentException>(() => new ServerPaths(packs, data));
        Assert.Contains("la ruta tiene que ser absoluta", ex.Message);
    }

    [Fact]
    public void La_base_y_los_archivos_de_platform()
    {
        var packs = Path.GetFullPath("/var/aida/packs");
        var data = Path.GetFullPath("/var/aida/data");
        var paths = new ServerPaths(packs, data);

        Assert.Equal(packs, paths.PacksDirectory);
        Assert.Equal(data, paths.DataDirectory);
        Assert.Equal(Path.Combine(data, "aida.db"), paths.DatabaseFile);
        Assert.Equal(Path.Combine(packs, "platform", "ui.yaml"), paths.PlatformPackFile("ui.yaml"));
    }

    [Fact]
    public void Pack_de_brazo_configuracion_y_clave()
    {
        var packs = Path.GetFullPath("/var/aida/packs");
        var data = Path.GetFullPath("/var/aida/data");
        var paths = new ServerPaths(packs, data);

        Assert.Equal(Path.Combine(packs, "agenda"), paths.ArmPackDirectory("agenda"));
        Assert.Equal(Path.Combine(data, "config", "agenda.yaml"), paths.ConfigFile("agenda.yaml"));
        Assert.Equal(Path.Combine(data, "keys", "machine.key"), paths.MachineKeyFile);
    }
}
