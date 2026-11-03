using Aida.Guards.Vocabulary;

namespace Aida.Guards.Tests;

public sealed class TextFoldTests
{
    [Theory]
    [InlineData("Diagnóstico", "diagnostico")]
    [InlineData("DIAGNÓSTICO", "diagnostico")]
    [InlineData("diagnostico", "diagnostico")]
    [InlineData("Año", "ano")]
    [InlineData("Cigüeña", "ciguena")]
    [InlineData("ÁÉÍÓÚáéíóú", "aeiouaeiou")]
    public void Pliega_tildes_dieresis_y_mayusculas(string entrada, string esperada)
    {
        Assert.Equal(esperada, TextFold.Fold(entrada));
    }
}
