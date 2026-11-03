using Aida.Guards.Vocabulary;

namespace Aida.Guards.Tests;

public sealed class WordSplitterTests
{
    [Theory]
    [InlineData("patientRecord", new[] { "patient", "record" })]
    [InlineData("HTTPServer", new[] { "http", "server" })]
    [InlineData("Cie10", new[] { "cie", "10" })]
    [InlineData("centinela_01", new[] { "centinela", "01" })]
    [InlineData("MisServidores", new[] { "mis", "servidores" })]
    [InlineData("TotalDisco", new[] { "total", "disco" })]
    [InlineData("escalarReplicas", new[] { "escalar", "replicas" })]
    [InlineData("alertaCritica", new[] { "alerta", "critica" })]
    public void Parte_identificadores_en_palabras_plegadas(string entrada, string[] esperadas)
    {
        var palabras = WordSplitter.Split(entrada);
        Assert.Equal(esperadas, palabras);
    }

    [Fact]
    public void Descarta_puntuacion_y_separadores_en_texto_libre()
    {
        var texto = "¡Hola, mundo! Esto es una prueba: 100% verificado; (diagnóstico rápido).";
        var palabras = WordSplitter.Split(texto);

        var esperadas = new[]
        {
            "hola", "mundo", "esto", "es", "una", "prueba", "100", "verificado", "diagnostico", "rapido",
        };

        Assert.Equal(esperadas, palabras);
    }
}
