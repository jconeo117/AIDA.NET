using Aida.Guards.Dependencies;

namespace Aida.Guards.Tests;

public sealed class DependencyGuardTests
{
    private readonly DependencyGuard _guard = new(DependencyPolicy.Allowed);

    [Fact]
    public void Brazo_que_referencia_a_otro_brazo_falla()
    {
        var violations = _guard.Check("Aida.Arms.Health", ["Aida.Arms.Agenda"]);

        var violation = Assert.Single(violations);
        Assert.Equal("C3", violation.Rule);
        Assert.Equal("Aida.Arms.Health", violation.Location);
        Assert.Equal(0, violation.Line);
        Assert.Contains("Aida.Arms.Agenda", violation.Message);
    }

    [Fact]
    public void Adaptador_que_referencia_a_un_brazo_falla()
    {
        var violations = _guard.Check("Aida.Capture", ["Aida.Arms.Health"]);

        var violation = Assert.Single(violations);
        Assert.Equal("C3", violation.Rule);
        Assert.Equal("Aida.Capture", violation.Location);
        Assert.Contains("Aida.Arms.Health", violation.Message);
    }

    [Fact]
    public void Aida_Engine_con_cualquier_referencia_falla()
    {
        var violations = _guard.Check("Aida.Engine", ["Aida.Platform"]);

        var violation = Assert.Single(violations);
        Assert.Equal("C3", violation.Rule);
        Assert.Equal("Aida.Engine", violation.Location);
        Assert.Contains("Aida.Platform", violation.Message);
    }

    [Fact]
    public void Aida_Web_que_referencia_Aida_Guards_falla()
    {
        var violations = _guard.Check("Aida.Web", ["Aida.Guards"]);

        var violation = Assert.Single(violations);
        Assert.Equal("C3", violation.Rule);
        Assert.Equal("Aida.Web", violation.Location);
        Assert.Contains("Aida.Guards", violation.Message);
    }

    [Fact]
    public void Proyecto_sin_politica_falla()
    {
        var violations = _guard.Check("Aida.Desconocido", ["Aida.Engine"]);

        var violation = Assert.Single(violations);
        Assert.Equal("C3", violation.Rule);
        Assert.Equal("Aida.Desconocido", violation.Location);
        Assert.Contains("no tiene política de dependencias", violation.Message);
    }

    [Fact]
    public void Referencias_permitidas_pasan_sin_violaciones()
    {
        var violations = _guard.Check("Aida.Platform", ["Aida.Engine"]);
        Assert.Empty(violations);

        var webViolations = _guard.Check("Aida.Web", ["Aida.Engine", "Aida.Platform", "Aida.Arms.Health"]);
        Assert.Empty(webViolations);
    }
}
