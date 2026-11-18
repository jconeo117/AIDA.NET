using System.Text;
using Aida.Platform.Keys;
using Aida.Platform.Sealing;
using Xunit;

namespace Aida.Platform.Tests.Sealing;

public sealed class SealerTests
{
    private static (Sealer Sealer, MachineKey Key) CreateSealer()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "aida-sealer-" + Guid.NewGuid().ToString("n"));
        var keyPath = Path.Combine(tempDir, "machine.key");
        var key = MachineKey.OpenOrCreate(keyPath, new DevelopmentKeyProtector());
        return (new Sealer(key), key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Hello, World!")]
    [InlineData("ñ😀")]
    public void Ida_y_vuelta(string text)
    {
        var (sealer, _) = CreateSealer();
        var sealedBytes = sealer.Seal(text);
        var opened = sealer.Open(sealedBytes);

        Assert.Equal(text, opened);
    }

    [Fact]
    public void Empieza_por_la_version_y_mide_29_mas_el_texto()
    {
        var (sealer, _) = CreateSealer();
        var text = "ñandú";
        var utf8Bytes = Encoding.UTF8.GetByteCount(text);
        var sealedBytes = sealer.Seal(text);

        Assert.Equal(1, sealedBytes[0]);
        Assert.Equal(29 + utf8Bytes, sealedBytes.Length);
    }

    [Fact]
    public void Dos_sellados_del_mismo_texto_difieren()
    {
        var (sealer, _) = CreateSealer();
        var text = "mismo texto";
        var s1 = sealer.Seal(text);
        var s2 = sealer.Seal(text);

        Assert.NotEqual(s1, s2);
        Assert.Equal(text, sealer.Open(s1));
        Assert.Equal(text, sealer.Open(s2));
    }

    [Fact]
    public void Otra_clave_no_abre()
    {
        var (sealer1, _) = CreateSealer();
        var (sealer2, _) = CreateSealer();

        var sealedBytes = sealer1.Seal("secreto");
        var ex = Assert.Throws<InvalidDataException>(() => sealer2.Open(sealedBytes));
        Assert.Contains("lo sellado no se abre", ex.Message);
    }

    [Fact]
    public void Un_byte_cambiado_no_abre()
    {
        var (sealer, _) = CreateSealer();
        var text = "un texto para probar bytes corruptos";
        var original = sealer.Seal(text);

        // 1. Corrupción en el nonce (índices 1 a 12)
        var corruptNonce = (byte[])original.Clone();
        corruptNonce[5] ^= 0xFF;
        var ex1 = Assert.Throws<InvalidDataException>(() => sealer.Open(corruptNonce));
        Assert.Contains("lo sellado no se abre", ex1.Message);

        // 2. Corrupción en el cifrado (índices 13 a 13 + text.Length - 1)
        var corruptCipher = (byte[])original.Clone();
        corruptCipher[15] ^= 0xFF;
        var ex2 = Assert.Throws<InvalidDataException>(() => sealer.Open(corruptCipher));
        Assert.Contains("lo sellado no se abre", ex2.Message);

        // 3. Corrupción en la etiqueta (últimos 16 bytes)
        var corruptTag = (byte[])original.Clone();
        corruptTag[^1] ^= 0xFF;
        var ex3 = Assert.Throws<InvalidDataException>(() => sealer.Open(corruptTag));
        Assert.Contains("lo sellado no se abre", ex3.Message);
    }

    [Fact]
    public void Corto_u_otra_version_no_abre()
    {
        var (sealer, _) = CreateSealer();

        // Demasiado corto (< 29 bytes)
        var shortBytes = new byte[28];
        shortBytes[0] = 1;
        var exShort = Assert.Throws<InvalidDataException>(() => sealer.Open(shortBytes));
        Assert.Contains("lo sellado no tiene el formato de la versión 1", exShort.Message);

        // Otra versión
        var valid = sealer.Seal("texto");
        valid[0] = 2;
        var exVer = Assert.Throws<InvalidDataException>(() => sealer.Open(valid));
        Assert.Contains("lo sellado no tiene el formato de la versión 1", exVer.Message);
    }
}
