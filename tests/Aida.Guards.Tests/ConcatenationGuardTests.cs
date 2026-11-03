using Aida.Guards.Vocabulary;

namespace Aida.Guards.Tests;

public sealed class ConcatenationGuardTests
{
    private readonly ConcatenationGuard _guard = new();

    [Fact]
    public void Concatenacion_de_literales_genera_violacion_A2_con_texto_unido()
    {
        var code = """
            public class Demo
            {
                public string Texto => "hola " + "mundo";
            }
            """;

        var violations = _guard.Check("src/Aida.Engine/Demo.cs", code);

        var violation = Assert.Single(violations);
        Assert.Equal("A2", violation.Rule);
        Assert.Equal("src/Aida.Engine/Demo.cs", violation.Location);
        Assert.Equal(3, violation.Line);
        Assert.Contains("hola mundo", violation.Message);
        Assert.Contains("Concatenación de literales", violation.Message);
    }

    [Fact]
    public void Codigo_sin_concatenaciones_de_literales_pasa()
    {
        var code = """"
            public class Demo
            {
                public string Texto => """texto crudo en varias lineas""";
            }
            """";

        var violations = _guard.Check("src/Aida.Engine/Demo.cs", code);
        Assert.Empty(violations);
    }
}
