using Aida.Engine.Packs;
using Aida.Engine.Tests.Golden;

namespace Aida.Engine.Tests.Packs;

public sealed class PackLoaderTests
{
    private const string BaseYaml = """
        id: base
        label: Base
        needs:
          - id: a
            label: A
            decay_per_tick: 0.1
          - id: b
            label: B
            decay_per_tick: 0.1
        traits:
          - id: t
            label: T
        actions:
          - id: x
            label: X
            reversibility: reversible
            duration_ticks: 1
            satisfies:
              a: 0.5
            effects:
              a: 0.5
        agents:
          - id: g
            label: G
        """;

    [Fact]
    public void Clave_repetida_falla_con_su_ruta()
    {
        var yaml = BaseYaml.Replace("decay_per_tick: 0.1", "decay_per_tick: 0.1\n    decay_per_tick: 0.2");
        var ex = Assert.Throws<PackException>(() => PackLoader.Parse(yaml));
        var problem = Assert.Single(ex.Problems);
        Assert.Equal(string.Empty, problem.Path);
        Assert.Contains("Duplicate key", problem.Message);
    }

    [Fact]
    public void Ancla_y_alias_fallan()
    {
        var yaml = BaseYaml.Replace("label: Base", "label: &lbl Base");
        var ex = Assert.Throws<PackException>(() => PackLoader.Parse(yaml));
        Assert.Contains(ex.Problems, p => p.Path == "label" && p.Message.Contains("Anchors and aliases"));
    }

    [Fact]
    public void Clave_de_fusion_falla()
    {
        var yaml = BaseYaml.Replace("label: Base", "label: Base\n<<: {}");
        var ex = Assert.Throws<PackException>(() => PackLoader.Parse(yaml));
        Assert.Contains(ex.Problems, p => p.Path == "<<" && p.Message.Contains("Merge keys"));
    }

    [Fact]
    public void Etiqueta_explicita_falla()
    {
        var yaml = BaseYaml.Replace("label: Base", "label: !!str Base");
        var ex = Assert.Throws<PackException>(() => PackLoader.Parse(yaml));
        Assert.Contains(ex.Problems, p => p.Path == "label" && p.Message.Contains("Explicit tags"));
    }

    [Fact]
    public void Entero_fuera_de_long_falla()
    {
        var yaml = BaseYaml.Replace(
            "id: a\n    label: A\n    decay_per_tick: 0.1",
            "id: a\n    label: A\n    decay_per_tick: 0.1\n    escalates_after: 9999999999999999999999999999999999999999");
        var ex = Assert.Throws<PackException>(() => PackLoader.Parse(yaml));
        Assert.Contains(ex.Problems, p => p.Path == "needs.0.escalates_after" && p.Message.Contains("64 bits"));
    }

    [Fact]
    public void Load_de_un_archivo_que_no_existe()
    {
        var ex = Assert.Throws<PackException>(() => PackLoader.Load("archivo_inexistente.yaml"));
        var problem = Assert.Single(ex.Problems);
        Assert.Equal(string.Empty, problem.Path);
        Assert.Contains("no se pudo leer el archivo", problem.Message);
    }

    [Fact]
    public void Los_indices_cubren_lo_declarado()
    {
        var packPath = Path.Combine(GoldenFiles.RepoRoot, "packs", "_plantilla", "pack.yaml");
        var pack = PackLoader.Load(packPath);

        Assert.Equal(pack.Needs.Length, pack.NeedById.Count);
        Assert.Equal(pack.Traits.Length, pack.TraitById.Count);
        Assert.Equal(pack.Situations.Length, pack.SituationById.Count);
        Assert.Equal(pack.Goals.Length, pack.GoalById.Count);

        foreach (var need in pack.Needs)
        {
            Assert.True(pack.NeedById.ContainsKey(need.Id));
            Assert.Same(need, pack.NeedById[need.Id]);
        }

        foreach (var trait in pack.Traits)
        {
            Assert.True(pack.TraitById.ContainsKey(trait.Id));
            Assert.Same(trait, pack.TraitById[trait.Id]);
        }

        foreach (var situation in pack.Situations)
        {
            Assert.True(pack.SituationById.ContainsKey(situation.Id));
            Assert.Same(situation, pack.SituationById[situation.Id]);
        }

        foreach (var goal in pack.Goals)
        {
            Assert.True(pack.GoalById.ContainsKey(goal.Id));
            Assert.Same(goal, pack.GoalById[goal.Id]);
        }
    }
}
