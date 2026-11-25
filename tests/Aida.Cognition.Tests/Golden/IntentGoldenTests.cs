using System.Text.Json;
using Aida.Cognition.Intent;
using Aida.Engine.Packs;
using Aida.Platform.Assistant;
using Xunit;

namespace Aida.Cognition.Tests.Golden;

public sealed class IntentGoldenTests
{
    private static readonly string[] DeclaredAgendaIntents = ["agendar", "ver_huecos", "cancelar", "reprogramar"];

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
    [Trait("Category", "Golden")]
    public void Clasificador_por_bolsa_de_palabras_coincide_con_la_01()
    {
        var repoRoot = FindRepoRoot();
        var goldenFile = Path.Combine(repoRoot, "tests", "golden", "intent", "cases.jsonl");
        var lines = File.ReadAllLines(goldenFile);

        var plantillaPath = Path.Combine(repoRoot, "packs", "_plantilla", "pack.yaml");
        var plantillaPack = PackLoader.Load(plantillaPath);
        var plantillaClf = BagOfWordsClassifier.FromPack(plantillaPack);
        Assert.NotNull(plantillaClf);

        var agendaPath = Path.Combine(repoRoot, "packs", "agenda", "intents.yaml");
        var agendaCatalog = IntentCatalog.Load(agendaPath, DeclaredAgendaIntents);
        var agendaClf = BagOfWordsClassifier.FromCatalog(agendaCatalog);

        var testedPlantilla = 0;
        var testedAgenda = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var kind = root.GetProperty("kind").GetString();

            if (kind == "plantilla_classify")
            {
                var phrase = root.GetProperty("phrase").GetString()!;
                var expected = root.GetProperty("expected_intent").GetString();
                var actual = plantillaClf.Classify(phrase);

                Assert.Equal(expected, actual);
                testedPlantilla++;
            }
            else if (kind == "agenda_classify")
            {
                var phrase = root.GetProperty("phrase").GetString()!;
                var expected = root.GetProperty("expected_intent").GetString();
                var actual = agendaClf.Classify(phrase);

                Assert.Equal(expected, actual);
                testedAgenda++;
            }
        }

        Assert.Equal(20, testedPlantilla);
        Assert.Equal(28, testedAgenda);

        // Verificación estricta del caso de empate (M1):
        // "agrega renueva" empata con 'pedir_escalar_replicas' y 'pedir_renovar_certificados'.
        // Con > estricto se queda la primera vista (id menor). Con >= ganaría la segunda.
        Assert.Equal("pedir_escalar_replicas", plantillaClf.Classify("agrega renueva"));
    }
}
