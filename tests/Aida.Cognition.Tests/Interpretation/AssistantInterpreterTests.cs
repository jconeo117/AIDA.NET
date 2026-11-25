using Aida.Cognition.Interpretation;
using Aida.Platform.Assistant;
using Xunit;

namespace Aida.Cognition.Tests.Interpretation;

public sealed class AssistantInterpreterTests
{
    private static readonly DateOnly Today = new(2026, 11, 24);

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
        Assert.Throws<ArgumentNullException>(() => interpreter.Interpret(null!, Today));
    }

    [Theory]
    [InlineData("cita para Ana Pérez mañana control", "cita=Intent,Ana=Free,Pérez=Free,mañana=Time,control=Free", null)]
    [InlineData("agenda a 300 555 1234", "agenda=Intent,300=Phone,555=Phone,1234=Phone", "3005551234")]
    [InlineData("cita para Juan a las 10", "cita=Intent,Juan=Free,a=Time,las=Time,10=Time", null)]
    [InlineData("pasado mañana el jueves", "pasado=Time,mañana=Time,el=Time,jueves=Time", null)]
    [InlineData("llame al 300-555-12", "llame=Free,300=Phone,555=Phone,12=Phone", "30055512")]
    [InlineData("llame al 300-55", "llame=Free,300=Number,55=Number", null)]
    [InlineData("cita 300 555 1234 o 311 222 3333", "cita=Intent,300=Phone,555=Phone,1234=Phone,311=Number,222=Number,3333=Number", "3005551234")]
    [InlineData("   ", "", null)]
    public void Interprete_asigna_papel_y_telefono_segun_contrato(string frase, string papelesEsperados, string? telefonoEsperado)
    {
        var interpreter = CreateRealInterpreter();
        var u = interpreter.Interpret(frase, Today);

        Assert.Equal(telefonoEsperado, u.Phone);

        var significativos = u.Words
            .Where(w => w.Role != WordRole.Filler)
            .Select(w => $"{w.Text}={w.Role}")
            .ToList();

        var esperados = string.IsNullOrEmpty(papelesEsperados)
            ? []
            : papelesEsperados.Split(',', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(esperados, significativos);
    }

    [Fact]
    public void Cita_para_Juan_a_las_10_marca_las_y_10_como_Time()
    {
        var interpreter = CreateRealInterpreter();
        var u = interpreter.Interpret("cita para Juan a las 10", Today);

        Assert.Equal(WordRole.Time, u.Words.First(w => w.Text == "las").Role);
        Assert.Equal(WordRole.Time, u.Words.First(w => w.Text == "10").Role);
    }

    [Fact]
    public void Llame_al_300_555_1234_mañana_mantiene_telefono()
    {
        var interpreter = CreateRealInterpreter();
        var u = interpreter.Interpret("llame al 300 555 1234 mañana", Today);

        Assert.Equal("3005551234", u.Phone);
        Assert.Equal(WordRole.Time, u.Words.First(w => w.Text == "mañana").Role);
    }

    [Fact]
    public void Cita_25_11_marca_25_y_11_como_Time_sin_telefono()
    {
        var interpreter = CreateRealInterpreter();
        var u = interpreter.Interpret("cita 25/11", Today);

        Assert.Null(u.Phone);
        Assert.Equal(WordRole.Time, u.Words.First(w => w.Text == "25").Role);
        Assert.Equal(WordRole.Time, u.Words.First(w => w.Text == "11").Role);
    }

    [Theory]
    [InlineData("llame al 3001234", "3001234")] // 7 dígitos exactos (MinPhoneDigits)
    [InlineData("llame al 300123456789012", "300123456789012")] // 15 dígitos exactos (MaxPhoneDigits)
    [InlineData("llame al 300123", null)] // 6 dígitos: muy corto
    [InlineData("llame al 3001234567890123", null)] // 16 dígitos: muy largo
    [InlineData("llame al 300.555.1234", "3005551234")] // Separador punto
    public void Limites_de_digitos_de_telefono(string frase, string? telefonoEsperado)
    {
        var interpreter = CreateRealInterpreter();
        var u = interpreter.Interpret(frase, Today);
        Assert.Equal(telefonoEsperado, u.Phone);
    }
}
