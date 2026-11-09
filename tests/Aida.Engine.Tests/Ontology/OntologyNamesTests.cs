using Aida.Engine.Ontology;

namespace Aida.Engine.Tests.Ontology;

public sealed class OntologyNamesTests
{
    [Fact]
    public void Reversibility_hacia_el_cable_y_desde_el_cable()
    {
        Assert.Equal("reversible", OntologyNames.ToWire(Reversibility.Reversible));
        Assert.Equal("reversible_with_cost", OntologyNames.ToWire(Reversibility.ReversibleWithCost));
        Assert.Equal("irreversible", OntologyNames.ToWire(Reversibility.Irreversible));
        Assert.Throws<ArgumentOutOfRangeException>(() => OntologyNames.ToWire((Reversibility)999));

        Assert.True(OntologyNames.TryParse("reversible", out Reversibility r1));
        Assert.Equal(Reversibility.Reversible, r1);
        Assert.True(OntologyNames.TryParse("reversible_with_cost", out Reversibility r2));
        Assert.Equal(Reversibility.ReversibleWithCost, r2);
        Assert.True(OntologyNames.TryParse("irreversible", out Reversibility r3));
        Assert.Equal(Reversibility.Irreversible, r3);
        Assert.False(OntologyNames.TryParse("desconocido", out Reversibility _));
    }

    [Fact]
    public void TemperamentKnob_hacia_el_cable_y_desde_el_cable()
    {
        Assert.Equal("temperature", OntologyNames.ToWire(TemperamentKnob.Temperature));
        Assert.Equal("loss_aversion", OntologyNames.ToWire(TemperamentKnob.LossAversion));
        Assert.Equal("critical_need_threshold", OntologyNames.ToWire(TemperamentKnob.CriticalThreshold));
        Assert.Equal("switch_cost", OntologyNames.ToWire(TemperamentKnob.SwitchCost));
        Assert.Equal("novelty_weight", OntologyNames.ToWire(TemperamentKnob.Novelty));
        Assert.Equal("learning_rate", OntologyNames.ToWire(TemperamentKnob.LearningRate));
        Assert.Throws<ArgumentOutOfRangeException>(() => OntologyNames.ToWire((TemperamentKnob)999));

        Assert.True(OntologyNames.TryParse("temperature", out TemperamentKnob k1));
        Assert.Equal(TemperamentKnob.Temperature, k1);
        Assert.True(OntologyNames.TryParse("learning_rate", out TemperamentKnob k2));
        Assert.Equal(TemperamentKnob.LearningRate, k2);
        Assert.False(OntologyNames.TryParse("desconocido", out TemperamentKnob _));
    }
}
