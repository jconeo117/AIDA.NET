using Aida.Arms.Health.Setup;
using Xunit;

namespace Aida.Arms.Health.Tests.Setup;

public sealed class EncounterTemplateTests
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
    public void Carga_el_pack_real_de_encounter_yaml()
    {
        var path = Path.Combine(FindRepoRoot(), "packs", "health", "encounter.yaml");
        var template = EncounterTemplate.Load(path);

        Assert.Equal("No consta.", template.NotRecorded);
        Assert.Equal(7, template.Sections.Count);
        Assert.Equal("motivo_consulta", template.Sections[0].Id);
        Assert.True(template.Sections[0].Required);
        Assert.Equal("enfermedad_actual", template.Sections[1].Id);
        Assert.Equal("antecedentes", template.Sections[2].Id);
        Assert.Equal("examen", template.Sections[3].Id);
        Assert.Equal("diagnostico", template.Sections[4].Id);
        Assert.Equal("plan", template.Sections[5].Id);
        Assert.Equal("observaciones", template.Sections[6].Id);
        Assert.False(template.Sections[6].Required);

        Assert.NotEmpty(template["title"]);
        Assert.NotEmpty(template["save_button"]);
        Assert.NotEmpty(template["sign_button"]);
        Assert.NotEmpty(template["password_label"]);

        Assert.Equal(template["reason_bad_password"], template.Reason("bad_password"));
        Assert.Equal(template["reason_failed"], template.Reason("motivo_inexistente"));
    }

    [Theory]
    [InlineData("sections: []\ntexts: {}", "falta 'not_recorded'")]
    [InlineData("not_recorded: 'x'\ntexts: {}", "falta la lista 'sections'")]
    [InlineData("not_recorded: 'x'\nsections:\n  - 'no es mapa'\ntexts: {}", "cada apartado es un mapa")]
    [InlineData("not_recorded: 'x'\nsections:\n  - required: 'true'\ntexts: {}", "falta 'id' en un apartado")]
    [InlineData("not_recorded: 'x'\nsections:\n  - id: '123_invalid'\ntexts: {}", "no es un identificador válido")]
    [InlineData("not_recorded: 'x'\nsections:\n  - id: 'dup'\n    required: 'true'\n    label: 'a'\n  - id: 'dup'\ntexts: {}", "aparece dos veces")]
    [InlineData("not_recorded: 'x'\nsections:\n  - id: 'sec'\n    required: 'maybe'\ntexts: {}", "'required' de «sec» debe ser true o false")]
    [InlineData("not_recorded: 'x'\nsections:\n  - id: 'sec'\n    required: 'true'\ntexts: {}", "falta 'label' en un apartado")]
    [InlineData("not_recorded: 'x'\nsections: []\n", "falta la sección 'texts'")]
    public void Valida_errores_de_estructura(string yaml, string errorEsperado)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, yaml);
            var ex = Assert.Throws<InvalidDataException>(() => EncounterTemplate.Load(tempFile));
            Assert.Contains(tempFile, ex.Message);
            Assert.Contains(errorEsperado, ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Falla_si_falta_alguna_clave_requerida_en_texts()
    {
        var validYaml = """
            not_recorded: "No consta."
            sections:
              - id: "motivo"
                label: "Motivo"
                required: true
            texts:
              title: "Historia"
            """;

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, validYaml);
            var ex = Assert.Throws<InvalidDataException>(() => EncounterTemplate.Load(tempFile));
            Assert.Contains(tempFile, ex.Message);
            Assert.Contains("faltan textos de la atención", ex.Message);
            Assert.Contains("card_link", ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
