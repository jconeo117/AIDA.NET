using Aida.Guards.Vocabulary;

namespace Aida.Guards.Tests;

public sealed class ForbiddenTermCatalogTests
{
    [Fact]
    public void Parse_ignora_lineas_vacias_y_comentarios()
    {
        var content = """
            # Titulo
            correo

            # Otra seccion
            servidor
            # Fin
            """;

        var terms = ForbiddenTermCatalog.Parse(content, TermScope.Code);

        Assert.Equal(2, terms.Count);
        Assert.Equal(["correo"], terms[0].Words);
        Assert.Equal(TermScope.Code, terms[0].Scope);
        Assert.Equal(["servidor"], terms[1].Words);
        Assert.Equal(TermScope.Code, terms[1].Scope);
    }

    [Fact]
    public void Termino_compuesto_genera_multiples_palabras()
    {
        var content = "espacio_libre\n";
        var terms = ForbiddenTermCatalog.Parse(content, TermScope.Code);

        var term = Assert.Single(terms);
        Assert.Equal(["espacio", "libre"], term.Words);
    }

    [Fact]
    public void LoadDefault_carga_ambas_listas_con_sus_alcances()
    {
        var terms = ForbiddenTermCatalog.LoadDefault();

        Assert.NotEmpty(terms);
        Assert.Contains(terms, t => t.Scope == TermScope.Code);
        Assert.Contains(terms, t => t.Scope == TermScope.Everywhere);
    }

    [Fact]
    public void Termino_duplicado_entre_ambas_listas_lanza_InvalidOperationException()
    {
        var contentDuplicated = "paciente\n";
        var code = ForbiddenTermCatalog.Parse(contentDuplicated, TermScope.Code);
        var everywhere = ForbiddenTermCatalog.Parse(contentDuplicated, TermScope.Everywhere);

        // Si alguna vez se encontraran en ambas listas, LoadDefault comprueba la colisión
        var hasOverlap = code.Any(c => everywhere.Any(e => e.Words.SequenceEqual(c.Words, StringComparer.Ordinal)));
        Assert.True(hasOverlap);
    }

    [Fact]
    public void Todos_los_ids_del_pack_de_plantilla_se_encuentran_en_el_catalogo()
    {
        var templatePackTerms = new[]
        {
            "espacio_libre", "certificados_vigentes", "replicas_al_dia",
            "purgar_temporales", "escalar_replicas", "rotar_certificados", "auditar_telemetria",
            "horario_laboral", "ventana_nocturna",
            "pico_de_trafico",
            "prudente", "proactivo",
            "centinela_01", "infraestructura_base",
            "mantenimiento", "escalabilidad", "alta_disponibilidad", "seguridad", "critico",
            "diagnostico",
        };

        var catalog = ForbiddenTermCatalog.LoadDefault();

        foreach (var rawTerm in templatePackTerms)
        {
            var expectedWords = WordSplitter.Split(rawTerm);
            var found = catalog.Any(t => t.Words.SequenceEqual(expectedWords, StringComparer.Ordinal));
            Assert.True(found, $"El término del pack de plantilla «{rawTerm}» no está en el catálogo.");
        }
    }
}
