using Aida.Engine.Disposition;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;

namespace Aida.Engine.Tests.Disposition;

public sealed class PressureTests
{
    [Fact]
    public void Urgency_responde_correctamente_en_los_casos_borde()
    {
        // cap <= 0 da 0.0
        Assert.Equal(0.0, Pressure.Urgency(0.5, 2.0, cap: 0.0));
        Assert.Equal(0.0, Pressure.Urgency(0.5, 2.0, cap: -1.0));

        // exponente 0 da 1.0 (cualquier cosa elevada a 0 es 1)
        Assert.Equal(1.0, Pressure.Urgency(0.5, exponent: 0.0, cap: 1.0));
        Assert.Equal(1.0, Pressure.Urgency(0.0, exponent: 0.0, cap: 1.0));

        // valor por encima del techo da 0.0 (recorte en 0 de 1 - valor / cap)
        Assert.Equal(0.0, Pressure.Urgency(1.2, exponent: 1.5, cap: 1.0));
        Assert.Equal(0.0, Pressure.Urgency(2.0, exponent: 1.0, cap: 0.5));
    }

    [Fact]
    public void ComputeStrain_sin_pesos_da_cero()
    {
        const string Yaml = """
            id: p1
            label: P1
            needs:
              - id: n1
                label: N1
                decay_per_tick: 0.1
                strain_weight: 0.0
            actions:
              - id: a1
                label: A1
                reversibility: reversible
                duration_ticks: 1
                satisfies:
                  n1: 0.5
                effects:
                  n1: 0.5
            agents:
              - id: ag1
                label: Ag1
            """;
        var pack = PackLoader.Parse(Yaml);
        var needId = new NeedId("n1");
        var needs = new Dictionary<NeedId, double> { [needId] = 0.1 };
        var strain = Pressure.ComputeStrain(pack, needs, null);

        Assert.Equal(0.0, strain);
    }
}
