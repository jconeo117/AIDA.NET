using Aida.Engine.Randomness;

namespace Aida.Engine.Tests.Randomness;

public sealed class WeightedChoiceTests
{
    [Fact]
    public void El_sorteo_usa_bisect_right_en_los_empates_exactos()
    {
        var mt = MersenneTwister.FromSeed(0);
        var r = mt.NextDouble();
        double[] weights = [r, 1.0 - r];

        var mtChoice = MersenneTwister.FromSeed(0);
        var choice = WeightedChoice.Pick(mtChoice, weights);

        Assert.Equal(1, choice);
    }

    [Fact]
    public void Pesos_que_suman_cero_lanzan()
    {
        var mt = MersenneTwister.FromSeed(0);
        double[] weights = [0.0, 0.0];

        var ex = Assert.Throws<ArgumentException>(() => WeightedChoice.Pick(mt, weights));
        Assert.Contains("Total of weights must be greater than zero", ex.Message);
    }

    [Fact]
    public void Pesos_no_finitos_lanzan()
    {
        var mt = MersenneTwister.FromSeed(0);
        double[] weights = [1.0, double.PositiveInfinity];

        var ex = Assert.Throws<ArgumentException>(() => WeightedChoice.Pick(mt, weights));
        Assert.Contains("Total of weights must be finite", ex.Message);
    }

    [Fact]
    public void Sin_pesos_lanza()
    {
        var mt = MersenneTwister.FromSeed(0);

        var ex = Assert.Throws<ArgumentException>(() => WeightedChoice.Pick(mt, []));
        Assert.Contains("No weights to choose from", ex.Message);
    }
}
