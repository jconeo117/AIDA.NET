using Aida.Platform.Keys;
using Xunit;

namespace Aida.Platform.Tests.Keys;

public sealed class KeyProtectorsTests
{
    [Fact]
    public void En_Windows_es_DPAPI_y_cierra_el_circulo()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var protector = KeyProtectors.ForHost(isDevelopment: false);
        Assert.IsType<DpapiKeyProtector>(protector);

        var secret = new byte[] { 1, 2, 3, 4, 5 };
        var protectedBytes = protector.Protect(secret);
        var unprotected = protector.Unprotect(protectedBytes);

        Assert.Equal(secret, unprotected);
    }

    [Fact]
    public void Fuera_de_Windows_en_desarrollo_es_el_de_desarrollo()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var protector = KeyProtectors.ForHost(isDevelopment: true);
        Assert.IsType<DevelopmentKeyProtector>(protector);

        var secret = new byte[] { 10, 20, 30 };
        var protectedBytes = protector.Protect(secret);
        Assert.Equal(secret, protectedBytes);
        Assert.Equal(secret, protector.Unprotect(protectedBytes));
    }

    [Fact]
    public void Fuera_de_Windows_en_produccion_lanza()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var ex = Assert.Throws<PlatformNotSupportedException>(() => KeyProtectors.ForHost(isDevelopment: false));
        Assert.Contains("solo se admite el entorno de desarrollo", ex.Message);
    }
}
