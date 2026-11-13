using Aida.Engine.Ontology;
using Aida.Engine.Randomness;

namespace Aida.Engine.Tests.Randomness;

public sealed class RngSourceTests
{
    [Fact]
    public void La_misma_clave_da_la_misma_secuencia()
    {
        var src = new RngSource(42);
        var mt1 = src.Stream("a", "b");
        var mt2 = src.Stream("a", "b");

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(mt1.NextUInt32(), mt2.NextUInt32());
            Assert.Equal(mt1.NextDouble(), mt2.NextDouble());
        }
    }

    [Fact]
    public void Partes_que_concatenan_igual_no_colisionan()
    {
        var src = new RngSource(42);
        var mt1 = src.Stream("ab", "c");
        var mt2 = src.Stream("a", "bc");

        Assert.NotEqual(mt1.NextUInt32(), mt2.NextUInt32());
    }

    [Fact]
    public void La_clave_de_decision_es_decide_agente_tick()
    {
        var src = new RngSource(42);
        var mtForDec = src.ForDecision(new AgentId("agente"), 15);
        var mtStream = src.Stream("decide", "agente", "15");

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(mtForDec.NextUInt32(), mtStream.NextUInt32());
            Assert.Equal(mtForDec.NextDouble(), mtStream.NextDouble());
        }
    }
}
