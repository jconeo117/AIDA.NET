using Aida.Platform.Assistant;
using Xunit;

namespace Aida.Platform.Tests.Assistant;

public sealed class IntentCatalogTests
{
    private static readonly string[] DeclaredIntents = ["agendar", "ver_huecos", "cancelar", "reprogramar"];

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
    public void Carga_catalogo_real_de_la_agenda()
    {
        var path = Path.Combine(FindRepoRoot(), "packs", "agenda", "intents.yaml");
        var catalog = IntentCatalog.Load(path, DeclaredIntents);

        Assert.Equal(0.25, catalog.Threshold);
        Assert.Contains("para", catalog.Stopwords);
        Assert.Contains("del", catalog.Stopwords);
        Assert.Equal(4, catalog.Intents.Count);
        Assert.Equal("agendar", catalog.Intents[0].Id);
        Assert.Equal("ver_huecos", catalog.Intents[1].Id);
        Assert.Equal("cancelar", catalog.Intents[2].Id);
        Assert.Equal("reprogramar", catalog.Intents[3].Id);
    }

    [Fact]
    public void Declared_null_lanza_ArgumentNullException()
    {
        var path = Path.Combine(FindRepoRoot(), "packs", "agenda", "intents.yaml");
        Assert.Throws<ArgumentNullException>(() => IntentCatalog.Load(path, null!));
    }

    [Fact]
    public void Stopwords_opcional_da_conjunto_vacio()
    {
        var temp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(temp, """
                threshold: 0.5
                intents:
                  - id: "agendar"
                    examples: ["cita"]
                """);

            var catalog = IntentCatalog.Load(temp, ["agendar"]);
            Assert.Empty(catalog.Stopwords);
            Assert.Single(catalog.Intents);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Theory]
    [InlineData("sin_threshold", "intents:\n  - id: 'agendar'\n    examples: ['cita']", "'threshold' tiene que ser un número en (0, 1]")]
    [InlineData("threshold_cero", "threshold: 0\nintents:\n  - id: 'agendar'\n    examples: ['cita']", "'threshold' tiene que ser un número en (0, 1]")]
    [InlineData("threshold_mayor_que_uno", "threshold: 1.1\nintents:\n  - id: 'agendar'\n    examples: ['cita']", "'threshold' tiene que ser un número en (0, 1]")]
    [InlineData("threshold_no_numero", "threshold: alto\nintents:\n  - id: 'agendar'\n    examples: ['cita']", "'threshold' tiene que ser un número en (0, 1]")]
    [InlineData("sin_intents", "threshold: 0.5\nstopwords: ['de']", "falta la lista 'intents' o está vacía")]
    [InlineData("intents_vacio", "threshold: 0.5\nintents: []", "falta la lista 'intents' o está vacía")]
    [InlineData("stopwords_no_lista", "threshold: 0.5\nstopwords: 'de'\nintents:\n  - id: 'agendar'\n    examples: ['cita']", "'stopwords' tiene que ser una lista de textos")]
    [InlineData("stopwords_elemento_vacio", "threshold: 0.5\nstopwords: ['']\nintents:\n  - id: 'agendar'\n    examples: ['cita']", "'stopwords' tiene un elemento vacío o que no es un texto")]
    [InlineData("intencion_no_mapa", "threshold: 0.5\nintents:\n  - 'agendar'", "cada intención es un mapa")]
    [InlineData("intencion_sin_id", "threshold: 0.5\nintents:\n  - examples: ['cita']", "una intención no tiene 'id'")]
    [InlineData("intencion_no_declarada", "threshold: 0.5\nintents:\n  - id: 'desconocida'\n    examples: ['cita']", "la intención 'desconocida' no está declarada")]
    [InlineData("intencion_repetida", "threshold: 0.5\nintents:\n  - id: 'agendar'\n    examples: ['cita']\n  - id: 'agendar'\n    examples: ['otra']", "la intención 'agendar' está repetida")]
    [InlineData("intencion_sin_ejemplos", "threshold: 0.5\nintents:\n  - id: 'agendar'\n    examples: []", "la intención 'agendar' no tiene ejemplos")]
    [InlineData("ejemplo_vacio", "threshold: 0.5\nintents:\n  - id: 'agendar'\n    examples: ['']", "la intención 'agendar' tiene un ejemplo vacío")]
    public void Valida_errores_de_formato_en_intents_yaml(string _, string yaml, string fragmentoEsperado)
    {
        var temp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(temp, yaml);
            var ex = Assert.Throws<InvalidDataException>(() => IntentCatalog.Load(temp, DeclaredIntents));
            Assert.StartsWith(temp, ex.Message);
            Assert.Contains(fragmentoEsperado, ex.Message);
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
