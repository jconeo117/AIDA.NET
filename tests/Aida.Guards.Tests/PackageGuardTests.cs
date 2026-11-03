using Aida.Guards.Dependencies;

namespace Aida.Guards.Tests;

public sealed class PackageGuardTests
{
    private readonly PackageGuard _guard = new(PackagePolicy.Rules);

    [Fact]
    public void PackageRule_Matches_reconoce_prefijos_con_punto_e_identidad_exacta()
    {
        var a1Rule = PackagePolicy.Rules.Single(r => r.Rule == "A1");

        Assert.True(a1Rule.Matches("Microsoft.Extensions.AI.Abstractions"));
        Assert.True(a1Rule.Matches("Microsoft.Extensions.AI"));
        Assert.True(a1Rule.Matches("OpenAI"));
        Assert.True(a1Rule.Matches("Mistral.Client"));
        Assert.False(a1Rule.Matches("MistralFoo"));
    }

    [Fact]
    public void OpenAI_falla_en_Aida_Engine_y_pasa_en_Aida_Cognition()
    {
        var engineViolations = _guard.Check("Aida.Engine", ["OpenAI"]);
        var violation = Assert.Single(engineViolations);
        Assert.Equal("A1", violation.Rule);
        Assert.Equal("Aida.Engine", violation.Location);
        Assert.Equal(0, violation.Line);
        Assert.Contains("El paquete OpenAI solo puede estar en", violation.Message);

        var cognitionViolations = _guard.Check("Aida.Cognition", ["OpenAI"]);
        Assert.Empty(cognitionViolations);
    }

    [Fact]
    public void NAudio_falla_en_Aida_Cognition_y_pasa_en_Aida_Capture()
    {
        var cognitionViolations = _guard.Check("Aida.Cognition", ["NAudio"]);
        var violation = Assert.Single(cognitionViolations);
        Assert.Equal("D4", violation.Rule);
        Assert.Equal("Aida.Cognition", violation.Location);
        Assert.Contains("El paquete NAudio solo puede estar en", violation.Message);

        var captureViolations = _guard.Check("Aida.Capture", ["NAudio"]);
        Assert.Empty(captureViolations);
    }

    [Fact]
    public void Paquetes_no_restringidos_pasan_en_cualquier_proyecto()
    {
        var violations = _guard.Check("Aida.Engine", ["xunit", "System.Text.Json"]);
        Assert.Empty(violations);
    }
}
