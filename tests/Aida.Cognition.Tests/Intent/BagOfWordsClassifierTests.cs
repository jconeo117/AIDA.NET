using Aida.Cognition.Intent;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;
using Aida.Platform.Assistant;
using Xunit;

namespace Aida.Cognition.Tests.Intent;

public sealed class BagOfWordsClassifierTests
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

    [Fact]
    public void FromPack_con_pack_sin_intenciones_devuelve_null()
    {
        var repoRoot = FindRepoRoot();
        var consultorioPath = Path.Combine(repoRoot, "packs", "health", "consultorio", "pack.yaml");
        var pack = PackLoader.Load(consultorioPath);
        Assert.True(pack.Intents.IsEmpty);

        var clf = BagOfWordsClassifier.FromPack(pack);
        Assert.Null(clf);
    }

    [Fact]
    public void Argumentos_null_lanzan_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => BagOfWordsClassifier.FromPack(null!));
        Assert.Throws<ArgumentNullException>(() => BagOfWordsClassifier.FromCatalog(null!));

        var repoRoot = FindRepoRoot();
        var agendaPath = Path.Combine(repoRoot, "packs", "agenda", "intents.yaml");
        var catalog = IntentCatalog.Load(agendaPath, ["agendar", "ver_huecos", "cancelar", "reprogramar"]);
        var clf = BagOfWordsClassifier.FromCatalog(catalog);
        Assert.Throws<ArgumentNullException>(() => clf.Classify(null!));
    }

    [Fact]
    public void Puntuacion_por_debajo_del_umbral_devuelve_null()
    {
        var repoRoot = FindRepoRoot();
        var agendaPath = Path.Combine(repoRoot, "packs", "agenda", "intents.yaml");
        var catalog = IntentCatalog.Load(agendaPath, ["agendar", "ver_huecos", "cancelar", "reprogramar"]);
        var clf = BagOfWordsClassifier.FromCatalog(catalog);

        // Frase sin palabras compartidas
        var result = clf.Classify("meteorología astronómica");
        Assert.Null(result);
    }

    [Fact]
    public void Frase_con_solo_palabras_vacias_devuelve_null()
    {
        var repoRoot = FindRepoRoot();
        var agendaPath = Path.Combine(repoRoot, "packs", "agenda", "intents.yaml");
        var catalog = IntentCatalog.Load(agendaPath, ["agendar", "ver_huecos", "cancelar", "reprogramar"]);
        var clf = BagOfWordsClassifier.FromCatalog(catalog);

        // "en el de la" solo tiene palabras vacías declaradas en el pack
        var result = clf.Classify("en el de la");
        Assert.Null(result);
    }

    [Fact]
    public void Frase_vacia_o_de_solo_espacios_devuelve_null()
    {
        var repoRoot = FindRepoRoot();
        var agendaPath = Path.Combine(repoRoot, "packs", "agenda", "intents.yaml");
        var catalog = IntentCatalog.Load(agendaPath, ["agendar", "ver_huecos", "cancelar", "reprogramar"]);
        var clf = BagOfWordsClassifier.FromCatalog(catalog);

        Assert.Null(clf.Classify(string.Empty));
        Assert.Null(clf.Classify("     "));
    }

    [Fact]
    public void FromCatalog_pliega_mayusculas_y_tildes()
    {
        var repoRoot = FindRepoRoot();
        var agendaPath = Path.Combine(repoRoot, "packs", "agenda", "intents.yaml");
        var catalog = IntentCatalog.Load(agendaPath, ["agendar", "ver_huecos", "cancelar", "reprogramar"]);
        var clf = BagOfWordsClassifier.FromCatalog(catalog);

        var r1 = clf.Classify("AGÉNDAME");
        var r2 = clf.Classify("agendame");

        Assert.Equal("agendar", r1);
        Assert.Equal(r1, r2);
    }
}
