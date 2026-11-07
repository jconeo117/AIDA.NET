namespace Aida.Guards.Tests;

public sealed class ViolationTests
{
    [Fact]
    public void ToString_con_linea_mayor_que_cero_incluye_la_linea()
    {
        var violation = new Violation("C1", "src/Test.cs", 42, "El archivo supera el límite.");
        Assert.Equal("[C1] src/Test.cs:42 — El archivo supera el límite.", violation.ToString());
    }

    [Fact]
    public void ToString_con_linea_cero_omite_el_numero_de_linea()
    {
        var violation = new Violation("C3", "Aida.Engine", 0, "Dependencia no permitida.");
        Assert.Equal("[C3] Aida.Engine — Dependencia no permitida.", violation.ToString());
    }
}
