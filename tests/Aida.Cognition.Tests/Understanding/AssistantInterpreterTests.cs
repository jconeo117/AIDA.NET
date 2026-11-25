using Aida.Cognition.Understanding;
using Aida.Platform.Assistant;
using Xunit;

namespace Aida.Cognition.Tests.Understanding;

public sealed class AssistantInterpreterTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

    private static AssistantInterpreter CreateRealInterpreter()
    {
        var repoRoot = FindRepoRoot();
        var catalogPath = Path.Combine(repoRoot, "packs", "agenda", "intents.yaml");
        var catalog = IntentCatalog.Load(catalogPath, ["agendar", "ver_huecos", "cancelar", "reprogramar"]);

        var timePath = Path.Combine(repoRoot, "packs", "platform", "time.yaml");
        var timeVocab = TimeVocabulary.Load(timePath);

        return new AssistantInterpreter(catalog, timeVocab);
    }

    [Fact]
    public void Constructor_valida_argumentos_null()
    {
        var repoRoot = FindRepoRoot();
        var catalogPath = Path.Combine(repoRoot, "packs", "agenda", "intents.yaml");
        var catalog = IntentCatalog.Load(catalogPath, ["agendar", "ver_huecos", "cancelar", "reprogramar"]);
        var timePath = Path.Combine(repoRoot, "packs", "platform", "time.yaml");
        var timeVocab = TimeVocabulary.Load(timePath);

        Assert.Throws<ArgumentNullException>(() => new AssistantInterpreter(null!, timeVocab));
        Assert.Throws<ArgumentNullException>(() => new AssistantInterpreter(catalog, null!));

        var interpreter = new AssistantInterpreter(catalog, timeVocab);
        Assert.Throws<ArgumentNullException>(() => interpreter.Interpret(null!));
    }

    [Theory]
    [InlineData("cita para Ana Pérez mañana control", "cita=Intent,Ana=Free,Pérez=Free,mañana=Time,control=Free", null)]
    [InlineData("agenda a 300 555 1234", "agenda=Intent,300=Phone,555=Phone,1234=Phone", "3005551234")]
    [InlineData("cita para Juan a las 10", "cita=Intent,Juan=Free,10=Number", null)]
    [InlineData("pasado mañana el jueves", "pasado=Time,mañana=Time,jueves=Time", null)]
    [InlineData("llame al 300-555-12", "llame=Free,300=Phone,555=Phone,12=Phone", "30055512")]
    [InlineData("llame al 300-55", "llame=Free,300=Number,55=Number", null)]
    [InlineData("cita 300 555 1234 o 311 222 3333", "cita=Intent,300=Phone,555=Phone,1234=Phone,311=Number,222=Number,3333=Number", "3005551234")]
    [InlineData("   ", "", null)]
    public void Interprete_asigna_papel_y_telefono_segun_contrato(string frase, string papelesEsperados, string? telefonoEsperado)
    {
        var interpreter = CreateRealInterpreter();
        var u = interpreter.Interpret(frase);

        Assert.Equal(telefonoEsperado, u.Phone);

        // Filtramos las palabras vacías (WordRole.Filler) para comparar los papeles significativos según la tabla de §B.2
        var significativos = u.Words
            .Where(w => w.Role != WordRole.Filler)
            .Select(w => $"{w.Text}={w.Role}")
            .ToList();

        var esperados = string.IsNullOrEmpty(papelesEsperados)
            ? []
            : papelesEsperados.Split(',', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(esperados, significativos);
    }
}
