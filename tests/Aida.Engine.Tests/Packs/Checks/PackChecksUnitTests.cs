using Aida.Engine.Packs;

namespace Aida.Engine.Tests.Packs.Checks;

public sealed class PackChecksUnitTests
{
    private static readonly string BaseYaml = """
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
        """.ReplaceLineEndings("\n");

    [Fact]
    public void La_base_de_los_tests_no_depende_del_checkout()
    {
        Assert.DoesNotContain('\r', BaseYaml);
    }

    [Fact]
    public void IdentityChecks_detecta_duplicados_en_todas_las_colecciones_y_mantiene_orden()
    {
        // 2 duplicados en necesidades (a y b) para matar OrderByDescending
        var yamlNeeds = BaseYaml
            .Replace("  - id: a\n    label: A\n    decay_per_tick: 0.1", "  - id: a\n    label: A1\n    decay_per_tick: 0.1\n  - id: a\n    label: A2\n    decay_per_tick: 0.1")
            .Replace("  - id: b\n    label: B\n    decay_per_tick: 0.1", "  - id: b\n    label: B1\n    decay_per_tick: 0.1\n  - id: b\n    label: B2\n    decay_per_tick: 0.1");
        var exNeeds = Assert.Throws<PackException>(() => PackLoader.Parse(yamlNeeds));
        Assert.Contains(exNeeds.Problems, p => p.Message.Contains("necesidades: id duplicado 'a'") && p.FromAida);
        Assert.Contains(exNeeds.Problems, p => p.Message.Contains("necesidades: id duplicado 'b'") && p.FromAida);

        // Duplicados en acciones
        var yamlActions = BaseYaml.Replace(
            "  - id: x",
            "  - id: x\n    label: X1\n    reversibility: reversible\n    duration_ticks: 1\n    satisfies:\n      a: 0.5\n    effects:\n      a: 0.5\n  - id: x");
        var exActions = Assert.Throws<PackException>(() => PackLoader.Parse(yamlActions));
        Assert.Contains(exActions.Problems, p => p.Message.Contains("acciones: id duplicado 'x'"));

        // Duplicados en agentes
        var yamlAgents = BaseYaml + "\n  - id: g\n    label: G2\n";
        var exAgents = Assert.Throws<PackException>(() => PackLoader.Parse(yamlAgents));
        Assert.Contains(exAgents.Problems, p => p.Message.Contains("agentes: id duplicado 'g'"));

        // Duplicados en rasgos
        var yamlTraits = BaseYaml.Replace("id: t\n    label: T", "id: t\n    label: T1\n  - id: t\n    label: T2");
        var exTraits = Assert.Throws<PackException>(() => PackLoader.Parse(yamlTraits));
        Assert.Contains(exTraits.Problems, p => p.Message.Contains("rasgos: id duplicado 't'"));

        // Duplicados en franjas
        var yamlPhases = BaseYaml + "\nphases:\n  - id: p\n    label: P1\n    starts_at: 0\n  - id: p\n    label: P2\n    starts_at: 10\n";
        var exPhases = Assert.Throws<PackException>(() => PackLoader.Parse(yamlPhases));
        Assert.Contains(exPhases.Problems, p => p.Message.Contains("franjas: id duplicado 'p'"));

        // Duplicados en situaciones
        var yamlSituations = BaseYaml + "\nsituations:\n  - id: s\n    label: S1\n  - id: s\n    label: S2\n";
        var exSituations = Assert.Throws<PackException>(() => PackLoader.Parse(yamlSituations));
        Assert.Contains(exSituations.Problems, p => p.Message.Contains("situaciones: id duplicado 's'"));

        // Duplicados en metas de pack
        var yamlGoals = BaseYaml + "\ngoals:\n  - id: m\n    label: M1\n    need: a\n    at_least: 0.5\n  - id: m\n    label: M2\n    need: a\n    at_least: 0.5\n";
        var exGoals = Assert.Throws<PackException>(() => PackLoader.Parse(yamlGoals));
        Assert.Contains(exGoals.Problems, p => p.Message.Contains("metas: id duplicado 'm'"));

        // Duplicados en intenciones
        var yamlIntents = BaseYaml + "\nintents:\n  - id: i\n    label: I1\n    need: a\n    examples: [\"e1\"]\n  - id: i\n    label: I2\n    need: a\n    examples: [\"e2\"]\n";
        var exIntents = Assert.Throws<PackException>(() => PackLoader.Parse(yamlIntents));
        Assert.Contains(exIntents.Problems, p => p.Message.Contains("intenciones: id duplicado 'i'"));

        // 2 colisiones entre acciones y agentes (g1 y g2) para matar OrderByDescending
        var yamlCol = """
            id: base
            label: Base
            needs:
              - id: a
                label: A
                decay_per_tick: 0.1
            traits:
              - id: t
                label: T
            actions:
              - id: g1
                label: A1
                reversibility: reversible
                duration_ticks: 1
                satisfies:
                  a: 0.5
                effects:
                  a: 0.5
              - id: g2
                label: A2
                reversibility: reversible
                duration_ticks: 1
                satisfies:
                  a: 0.5
                effects:
                  a: 0.5
            agents:
              - id: g1
                label: G1
              - id: g2
                label: G2
            """.ReplaceLineEndings("\n");
        var exCol = Assert.Throws<PackException>(() => PackLoader.Parse(yamlCol));
        var pCol = exCol.Problems.Where(p => p.Message.Contains("usado a la vez por un/a acción y un/a agente")).ToList();
        Assert.Equal(2, pCol.Count);
        Assert.Contains("'g1'", pCol[0].Message);
        Assert.Contains("'g2'", pCol[1].Message);
    }

    [Fact]
    public void ReferenceChecks_detecta_referencias_desconocidas()
    {
        // Necesidad desconocida en rasgos (multiplicadores)
        var yamlTrait = BaseYaml.Replace(
            "label: T",
            "label: T\n    need_weight_multipliers:\n      z1: 1.5\n      z2: 2.0\n    gain_multipliers:\n      z3: 1.5\n    cost_multipliers:\n      z4: 1.5\n    cap_multipliers:\n      z5: 1.5");
        var exTrait = Assert.Throws<PackException>(() => PackLoader.Parse(yamlTrait));
        Assert.Contains(exTrait.Problems, p => p.Message.Contains("rasgo 't': necesidad desconocida"));

        // Necesidad desconocida en situaciones
        var yamlSit = BaseYaml + "\nsituations:\n  - id: s\n    label: S\n    gain_multipliers:\n      z: 1.5\n    effects:\n      z2: 0.5\n";
        var exSit = Assert.Throws<PackException>(() => PackLoader.Parse(yamlSit));
        Assert.Contains(exSit.Problems, p => p.Message.Contains("situación 's'.gain_multipliers: necesidad desconocida"));
        Assert.Contains(exSit.Problems, p => p.Message.Contains("situación 's'.effects: necesidad desconocida"));

        // Rasgo desconocido en accion
        var yamlActTrait = BaseYaml.Replace("effects:\n      a: 0.5", "effects:\n      a: 0.5\n    requires_traits:\n      z_trait: 0.5");
        var exActTrait = Assert.Throws<PackException>(() => PackLoader.Parse(yamlActTrait));
        Assert.Contains(exActTrait.Problems, p => p.Message.Contains("acción 'x'.requires_traits: rasgo desconocida"));
    }

    [Fact]
    public void SettingsChecks_detecta_cotas_y_referencias_en_salud()
    {
        // may_go_unused con 2 acciones desconocidas (ordenadas)
        var yamlHealth = BaseYaml + "\nhealth:\n  may_go_unused:\n    - z2\n    - z1\n";
        var exHealth = Assert.Throws<PackException>(() => PackLoader.Parse(yamlHealth));
        var pHealth = exHealth.Problems.Where(p => p.Message.Contains("health.may_go_unused")).ToList();
        Assert.Equal(2, pHealth.Count);
        Assert.Contains("'z1'", pHealth[0].Message);
        Assert.Contains("'z2'", pHealth[1].Message);

        // initial_needs fuera de [0, 1] (ordenadas)
        var yamlBounds = BaseYaml + "\n    initial_needs:\n      b: 1.5\n      a: -0.1\n";
        var exBounds = Assert.Throws<PackException>(() => PackLoader.Parse(yamlBounds));
        var pBounds = exBounds.Problems.Where(p => p.Message.Contains("initial_needs")).ToList();
        Assert.Equal(2, pBounds.Count);
        Assert.Contains("['a'] = -0.1", pBounds[0].Message);
        Assert.Contains("['b'] = 1.5", pBounds[1].Message);

        // at_traits fuera de [-2, 2] en metas de pack
        var yamlGoalBounds = BaseYaml + "\ngoals:\n  - id: m\n    label: M\n    need: a\n    at_traits:\n      t: 2.5\n";
        var exGoalBounds = Assert.Throws<PackException>(() => PackLoader.Parse(yamlGoalBounds));
        Assert.Contains(exGoalBounds.Problems, p => p.Message.Contains("meta 'm'.at_traits['t'] = 2.5, fuera de [-2.0, 2.0]"));
    }

    [Fact]
    public void ActionChecks_detecta_acciones_inalcanzables_y_costes_no_declarados()
    {
        // 2 efectos negativos sin coste (orden)
        var yamlCosts = BaseYaml.Replace(
            "effects:\n      a: 0.5",
            "effects:\n      a: 0.5\n      b: -0.1\n  - id: y\n    label: Y\n    reversibility: reversible\n    duration_ticks: 1\n    satisfies:\n      a: 0.5\n    effects:\n      a: 0.5\n      b: -0.2\n");
        var exCosts = Assert.Throws<PackException>(() => PackLoader.Parse(yamlCosts));
        Assert.Contains(exCosts.Problems, p => p.Message.Contains("acción 'x': el efecto sobre 'b' es negativo"));
        Assert.Contains(exCosts.Problems, p => p.Message.Contains("acción 'y': el efecto sobre 'b' es negativo"));

        // Acción requiere rasgo que ningún agente alcanza
        var yamlReqTrait = BaseYaml.Replace("effects:\n      a: 0.5", "effects:\n      a: 0.5\n    requires_traits:\n      t: 0.8");
        var exReqTrait = Assert.Throws<PackException>(() => PackLoader.Parse(yamlReqTrait));
        Assert.Contains(exReqTrait.Problems, p => p.Message.Contains("acción 'x': exige rasgo 't' a 0.8, pero ningún agente lo alcanza"));
    }

    [Fact]
    public void GoalChecks_detecta_metas_de_pack_con_referencias_invalidas_o_inalcanzables()
    {
        // Meta de pack con necesidad desconocida
        var yamlNeed = BaseYaml + "\ngoals:\n  - id: m\n    label: M\n    need: z_need\n    at_least: 0.5\n";
        var exNeed = Assert.Throws<PackException>(() => PackLoader.Parse(yamlNeed));
        Assert.Contains(exNeed.Problems, p => p.Message.Contains("meta 'm': necesidad desconocida 'z_need'"));

        // Meta de pack con rasgo desconocido en on_success / on_failure
        var yamlTrait = BaseYaml + "\ngoals:\n  - id: m\n    label: M\n    need: a\n    at_least: 0.5\n    on_success:\n      z_trait: 1.0\n    on_failure:\n      z_trait2: -1.0\n";
        var exTrait = Assert.Throws<PackException>(() => PackLoader.Parse(yamlTrait));
        Assert.Contains(exTrait.Problems, p => p.Message.Contains("meta 'm'.on_success: rasgo desconocida/o 'z_trait'"));
        Assert.Contains(exTrait.Problems, p => p.Message.Contains("meta 'm'.on_failure: rasgo desconocida/o 'z_trait2'"));

        // Meta de pack inalcanzable (pide rasgo y ninguna meta lo sube)
        var yamlUnreach = BaseYaml + "\ngoals:\n  - id: m\n    label: M\n    need: a\n    at_traits:\n      t: 0.5\n";
        var exUnreach = Assert.Throws<PackException>(() => PackLoader.Parse(yamlUnreach));
        Assert.Contains(exUnreach.Problems, p => p.Message.Contains("meta 'm': pide 't' a 0.5 y ninguna meta se lo sube"));

        // Ciclo en metas de pack (m1 -> m2 -> m1)
        var yamlCycle = BaseYaml + "\ngoals:\n  - id: m1\n    label: M1\n    need: a\n    at_least: 0.5\n    requires_goal: m2\n  - id: m2\n    label: M2\n    need: a\n    at_least: 0.5\n    requires_goal: m1\n";
        var exCycle = Assert.Throws<PackException>(() => PackLoader.Parse(yamlCycle));
        Assert.Contains(exCycle.Problems, p => p.Message.Contains("ciclo de metas"));
    }
}
