using Aida.Engine.Authorization;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;

namespace Aida.Engine.Tests.Authorization;

public sealed class AuthorityTests
{
    private const string Yaml = """
        id: p_auth
        label: Authority Pack
        needs:
          - id: n1
            label: N1
            decay_per_tick: 0.1
        actions:
          - id: act_rev
            label: Rev
            reversibility: reversible
            duration_ticks: 1
            satisfies:
              n1: 0.1
            effects:
              n1: 0.1
          - id: b_act
            label: Cost B
            reversibility: reversible_with_cost
            duration_ticks: 1
            satisfies:
              n1: 0.1
            effects:
              n1: 0.1
          - id: a_act
            label: Cost A
            reversibility: reversible_with_cost
            duration_ticks: 1
            satisfies:
              n1: 0.1
            effects:
              n1: 0.1
          - id: c_act
            label: Irrev C
            reversibility: irreversible
            duration_ticks: 1
            satisfies:
              n1: 0.1
            effects:
              n1: 0.1
        agents:
          - id: ag1
            label: Agent 1
            initial_needs:
              n1: 0.5
        """;

    private readonly ContentPack _pack = PackLoader.Parse(Yaml);

    [Fact]
    public void El_techo_usa_el_rango_y_no_el_orden_del_enum()
    {
        var rev = _pack.ActionById[new ActionId("act_rev")];
        var cost = _pack.ActionById[new ActionId("a_act")];
        var irrev = _pack.ActionById[new ActionId("c_act")];

        var authRev = new Authority(new AuthoritySpec(Reversibility.Reversible, 10));
        Assert.False(authRev.IsGated(rev));
        Assert.True(authRev.MayStart(rev));
        Assert.True(authRev.IsGated(cost));
        Assert.False(authRev.MayStart(cost));
        Assert.True(authRev.IsGated(irrev));
        Assert.False(authRev.MayStart(irrev));

        var authCost = new Authority(new AuthoritySpec(Reversibility.ReversibleWithCost, 10));
        Assert.False(authCost.IsGated(rev));
        Assert.True(authCost.MayStart(rev));
        Assert.False(authCost.IsGated(cost));
        Assert.True(authCost.MayStart(cost));
        Assert.True(authCost.IsGated(irrev));
        Assert.False(authCost.MayStart(irrev));

        var authIrrev = new Authority(new AuthoritySpec(Reversibility.Irreversible, 10));
        Assert.False(authIrrev.IsGated(rev));
        Assert.True(authIrrev.MayStart(rev));
        Assert.False(authIrrev.IsGated(cost));
        Assert.True(authIrrev.MayStart(cost));
        Assert.False(authIrrev.IsGated(irrev));
        Assert.True(authIrrev.MayStart(irrev));
    }

    [Fact]
    public void Una_autorizacion_sirve_una_sola_vez()
    {
        var cost = _pack.ActionById[new ActionId("a_act")];
        var auth = new Authority(new AuthoritySpec(Reversibility.Reversible, 10));

        Assert.True(auth.Propose(cost, 5));
        Assert.True(auth.Approve(cost.Id));

        Assert.True(auth.MayStart(cost));
        Assert.False(auth.MayStart(cost));
    }

    [Fact]
    public void Una_propuesta_viva_no_se_renueva()
    {
        var cost = _pack.ActionById[new ActionId("a_act")];
        var auth = new Authority(new AuthoritySpec(Reversibility.Reversible, 10));

        Assert.True(auth.Propose(cost, 5));
        Assert.Equal(15, auth.Pending[cost.Id]);

        Assert.False(auth.Propose(cost, 8));
        Assert.Equal(15, auth.Pending[cost.Id]);
    }

    [Fact]
    public void No_se_autoriza_lo_que_nadie_propuso()
    {
        var cost = _pack.ActionById[new ActionId("a_act")];
        var auth = new Authority(new AuthoritySpec(Reversibility.Reversible, 10));

        Assert.False(auth.Approve(cost.Id));
        Assert.False(auth.MayStart(cost));
    }

    [Fact]
    public void Rechazar_retira_la_propuesta()
    {
        var cost = _pack.ActionById[new ActionId("a_act")];
        var auth = new Authority(new AuthoritySpec(Reversibility.Reversible, 10));

        Assert.True(auth.Propose(cost, 5));
        Assert.True(auth.Deny(cost.Id));
        Assert.False(auth.Pending.ContainsKey(cost.Id));

        Assert.False(auth.Deny(cost.Id));
        Assert.False(auth.MayStart(cost));
    }

    [Fact]
    public void Caduca_justo_en_su_tick_y_en_orden_de_id()
    {
        var aAct = _pack.ActionById[new ActionId("a_act")];
        var bAct = _pack.ActionById[new ActionId("b_act")];
        var cAct = _pack.ActionById[new ActionId("c_act")];
        var auth = new Authority(new AuthoritySpec(Reversibility.Reversible, 5));

        Assert.True(auth.Propose(bAct, 10));
        Assert.True(auth.Propose(aAct, 10));
        Assert.True(auth.Propose(cAct, 11));

        Assert.Empty(auth.Expired(14));
        Assert.Equal(3, auth.Pending.Count);

        var exp15 = auth.Expired(15);
        Assert.Equal(new[] { aAct.Id, bAct.Id }, exp15);
        Assert.Single(auth.Pending);
        Assert.True(auth.Pending.ContainsKey(cAct.Id));

        var exp16 = auth.Expired(16);
        Assert.Equal(new[] { cAct.Id }, exp16);
        Assert.Empty(auth.Pending);
    }
}
