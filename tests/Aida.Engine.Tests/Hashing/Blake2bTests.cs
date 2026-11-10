using Aida.Engine.Hashing;

namespace Aida.Engine.Tests.Hashing;

public sealed class Blake2bTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65)]
    [InlineData(100)]
    public void Blake2b_rechaza_tamanos_fuera_de_rango(int invalidSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Blake2b.Hash(ReadOnlySpan<byte>.Empty, invalidSize));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(64)]
    public void Blake2b_acepta_tamanos_validos(int validSize)
    {
        var hash = Blake2b.Hash(ReadOnlySpan<byte>.Empty, validSize);
        Assert.Equal(validSize, hash.Length);
    }
}
