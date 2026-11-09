using Aida.Engine.Packs;

namespace Aida.Engine.Tests.Packs;

public sealed class PackExceptionTests
{
    [Fact]
    public void Mensaje_resume_problemas_con_y_sin_ruta()
    {
        var p1 = new PackProblem("needs.0.decay_per_tick", "fuera de rango", false);
        var p2 = new PackProblem(string.Empty, "error global", true);
        var ex = new PackException([p1, p2]);

        Assert.Equal(2, ex.Problems.Length);
        Assert.Equal("pack inválido:\n  - needs.0.decay_per_tick: fuera de rango\n  - error global", ex.Message);
    }
}
