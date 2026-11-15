using Aida.Platform.Identity;
using Xunit;

namespace Aida.Platform.Tests.Identity;

public sealed class PasswordHasherTests
{
    [Fact]
    public void La_huella_lleva_esquema_iteraciones_sal_y_huella()
    {
        var hasher = new PasswordHasher(10);
        var hash = hasher.Hash("contraseña-secreta");

        var parts = hash.Split('$');
        Assert.Equal(4, parts.Length);
        Assert.Equal("pbkdf2-sha256", parts[0]);
        Assert.Equal("10", parts[1]);
        Assert.Equal(32, parts[2].Length); // 16 bytes hex = 32 chars
        Assert.Equal(64, parts[3].Length); // 32 bytes hex = 64 chars

        Assert.True(hasher.Verify("contraseña-secreta", hash));
        Assert.False(hasher.Verify("otra-contraseña", hash));
    }

    [Fact]
    public void La_misma_contrasena_con_otra_sal_da_otra_huella()
    {
        var hasher = new PasswordHasher(10);
        var hash1 = hasher.Hash("contraseña-secreta");
        var hash2 = hasher.Hash("contraseña-secreta");

        Assert.NotEqual(hash1, hash2);
        Assert.True(hasher.Verify("contraseña-secreta", hash1));
        Assert.True(hasher.Verify("contraseña-secreta", hash2));
    }

    [Theory]
    [InlineData("pbkdf2-sha256$10$aabb")] // partes de menos
    [InlineData("otro-esquema$10$0123456789abcdef0123456789abcdef$0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")] // otro esquema
    [InlineData("pbkdf2-sha256$0$0123456789abcdef0123456789abcdef$0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")] // iteraciones 0
    [InlineData("pbkdf2-sha256$diez$0123456789abcdef0123456789abcdef$0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")] // iteraciones no numéricas
    [InlineData("pbkdf2-sha256$10$no-es-hexadecimal!$0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")] // sal no hexadecimal
    [InlineData("pbkdf2-sha256$10$0123456789abcdef0123456789abcdef$no-es-hexadecimal!")] // huella no hexadecimal
    public void Una_huella_ilegible_no_coincide_nunca(string stored)
    {
        var hasher = new PasswordHasher(10);
        Assert.False(hasher.Verify("cualquier-contrasena", stored));
    }

    [Fact]
    public void El_servidor_usa_seiscientas_mil_iteraciones()
    {
        Assert.Equal(600_000, PasswordHasher.ServerIterations);
    }
}
