using Aida.Guards.Vocabulary;

namespace Aida.Guards.Tests;

public sealed class VocabularyGuardTests
{
    private readonly VocabularyGuard _guard = new(ForbiddenTermCatalog.LoadDefault());

    [Fact]
    public void Porta_test_agnosticism_cazando_compuestos_y_plurales_en_identificadores()
    {
        var code = """
            public sealed class TotalDisco { }
            public sealed class Componente
            {
                public void EscalarReplicas() { }
                public int MisServidores { get; set; }
                public void AlertaCritica() { }
            }
            """;

        var violations = _guard.Check("src/Aida.Engine/Componente.cs", code);

        Assert.Contains(violations, v => v.Message.Contains("disco"));
        Assert.Contains(violations, v => v.Message.Contains("escalar replicas") || v.Message.Contains("replicas"));
        Assert.Contains(violations, v => v.Message.Contains("servidores"));
        Assert.Contains(violations, v => v.Message.Contains("alerta") || v.Message.Contains("critico"));
    }

    [Fact]
    public void PatientRecord_en_identificador_falla()
    {
        var code = "public class PatientRecord { }";
        var violations = _guard.Check("src/Aida.Engine/PatientRecord.cs", code);

        var violation = Assert.Single(violations);
        Assert.Equal("A2", violation.Rule);
        Assert.Equal("src/Aida.Engine/PatientRecord.cs", violation.Location);
        Assert.Contains("patient", violation.Message);
    }

    [Fact]
    public void Param_no_casa_con_ram()
    {
        var code = "public void Execute(int param) { }";
        var violations = _guard.Check("src/Aida.Engine/Engine.cs", code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Cadena_con_termino_prohibido_falla()
    {
        var code = """var texto = "Paciente";""";
        var violations = _guard.Check("src/Aida.Engine/Engine.cs", code);

        var violation = Assert.Single(violations);
        Assert.Equal("A2", violation.Rule);
        Assert.Contains("paciente", violation.Message);
    }

    [Fact]
    public void Comentario_de_linea_se_guarda_en_disco_pasa()
    {
        var code = """
            // se guarda en disco para depuracion
            public class MotorBase { }
            """;

        var violations = _guard.Check("src/Aida.Engine/MotorBase.cs", code);

        Assert.Empty(violations);
    }

    [Fact]
    public void Comentario_xml_con_termino_everywhere_falla()
    {
        var code = """
            /// <summary>El paciente debe esperar al resultado.</summary>
            public class Consulta { }
            """;

        var violations = _guard.Check("src/Aida.Engine/Consulta.cs", code);

        var violation = Assert.Single(violations);
        Assert.Equal("A2", violation.Rule);
        Assert.Contains("paciente", violation.Message);
    }

    [Fact]
    public void Concatenacion_partida_de_termino_falla_por_el_termino_completo()
    {
        var code = """var resultado = "c" + "pu";""";
        var violations = _guard.Check("src/Aida.Engine/Engine.cs", code);

        Assert.Contains(violations, v => v.Rule == "A2" && v.Message.Contains("cpu"));
    }
}
