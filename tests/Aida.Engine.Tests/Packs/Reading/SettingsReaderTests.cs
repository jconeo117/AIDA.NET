using Aida.Engine.Ontology;
using Aida.Engine.Packs.Reading;
using YamlDotNet.RepresentationModel;

namespace Aida.Engine.Tests.Packs.Reading;

public sealed class SettingsReaderTests
{
    [Fact]
    public void DefaultSettings_tienen_valores_esperados()
    {
        var auth = SettingsReader.DefaultAuthority();
        Assert.Equal(Reversibility.Reversible, auth.AutonomousUpTo);
        Assert.Equal(48, auth.ConfirmationTtlTicks);

        var health = SettingsReader.DefaultHealth();
        Assert.Equal(7, health.Days);
        Assert.Equal(new long[] { 1312, 7, 99 }, health.Seeds);
        Assert.Null(health.MinCrisis);
        Assert.Null(health.MaxCrisis);
        Assert.Null(health.MaxAbortRatio);
        Assert.Null(health.MinDifferentiation);
        Assert.Empty(health.MayGoUnused);

        var tuning = SettingsReader.DefaultTuning();
        Assert.Equal(3, tuning.CandidatePool);
        Assert.Equal(0.15, tuning.Temperature);
        Assert.Equal(0.004, tuning.SaturationRecovery);
        Assert.Equal(0.6, tuning.NoveltyWeight);
        Assert.Equal(1.0, tuning.LossAversion);
        Assert.Equal(0.0, tuning.LearningRate);
        Assert.Empty(tuning.StrainMultipliers);
        Assert.Equal(1.0, tuning.DurationDiscount);
        Assert.Equal(0.0, tuning.SwitchCost);
        Assert.Equal(0.15, tuning.CriticalNeedThreshold);
        Assert.Equal(0.35, tuning.IntentConfidenceThreshold);
    }

    [Fact]
    public void TryReadAuthority_lee_valores_y_detecta_errores()
    {
        var problems = new ProblemList();
        var map = new YamlMappingNode
        {
            { new YamlScalarNode("autonomous_up_to"), new YamlScalarNode("irreversible") },
            { new YamlScalarNode("confirmation_ttl_ticks"), new YamlScalarNode("24") }
        };

        Assert.True(SettingsReader.TryReadAuthority(map, "authority", problems, out var auth));
        Assert.Equal(Reversibility.Irreversible, auth.AutonomousUpTo);
        Assert.Equal(24, auth.ConfirmationTtlTicks);

        Assert.False(SettingsReader.TryReadAuthority(new YamlScalarNode("no"), "authority", problems, out _));
    }

    [Fact]
    public void TryReadHealth_lee_opcionales_y_listas()
    {
        var problems = new ProblemList();
        var map = new YamlMappingNode
        {
            { new YamlScalarNode("days"), new YamlScalarNode("14") },
            { new YamlScalarNode("min_crisis"), new YamlScalarNode("0.1") },
            { new YamlScalarNode("max_crisis"), new YamlScalarNode("0.9") },
            { new YamlScalarNode("max_abort_ratio"), new YamlScalarNode("0.5") },
            { new YamlScalarNode("min_differentiation"), new YamlScalarNode("0.2") },
            {
                new YamlScalarNode("may_go_unused"),
                new YamlSequenceNode(new YamlScalarNode("b_act"), new YamlScalarNode("a_act"), new YamlScalarNode("a_act"))
            }
        };

        Assert.True(SettingsReader.TryReadHealth(map, "health", problems, out var health));
        Assert.Equal(14, health.Days);
        Assert.Equal(0.1, health.MinCrisis);
        Assert.Equal(0.9, health.MaxCrisis);
        Assert.Equal(0.5, health.MaxAbortRatio);
        Assert.Equal(0.2, health.MinDifferentiation);
        // Deduplicadas y ordenadas
        Assert.Equal(2, health.MayGoUnused.Length);
        Assert.Equal("a_act", health.MayGoUnused[0].Value);
        Assert.Equal("b_act", health.MayGoUnused[1].Value);
    }

    [Fact]
    public void TryReadTuning_lee_valores_y_rechaza_no_mapa()
    {
        var problems = new ProblemList();
        Assert.False(SettingsReader.TryReadTuning(new YamlScalarNode("no"), "tuning", problems, out _));

        var map = new YamlMappingNode
        {
            { new YamlScalarNode("candidate_pool"), new YamlScalarNode("5") },
            { new YamlScalarNode("temperature"), new YamlScalarNode("0.25") },
        };
        Assert.True(SettingsReader.TryReadTuning(map, "tuning", problems, out var tuning));
        Assert.Equal(5, tuning.CandidatePool);
        Assert.Equal(0.25, tuning.Temperature);
    }
}
