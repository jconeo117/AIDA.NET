using System.Collections;
using Aida.Engine.Events;
using Aida.Engine.Ontology;

namespace Aida.Engine.Tests.Events;

public sealed class EventLogTests
{
    [Fact]
    public void Emit_numera_desde_cero_de_uno_en_uno()
    {
        var log = new EventLog();
        var agent = new AgentId("agente-1");

        var e0 = log.Emit(10, EventKind.ActionStarted, agent);
        var e1 = log.Emit(11, EventKind.ActionCompleted, agent);
        var e2 = log.Emit(12, EventKind.ActionRequested, agent);

        Assert.Equal(0, e0.Seq);
        Assert.Equal(1, e1.Seq);
        Assert.Equal(2, e2.Seq);

        Assert.Equal(3, log.Count);

        var list = new List<Event>(log);
        Assert.Equal(3, list.Count);
        Assert.Same(e0, list[0]);
        Assert.Same(e1, list[1]);
        Assert.Same(e2, list[2]);

        // Cobertura del enumerador no genérico
        var nonGeneric = ((IEnumerable)log).GetEnumerator();
        Assert.True(nonGeneric.MoveNext());
        Assert.Same(e0, nonGeneric.Current);
    }

    [Fact]
    public void Emit_rechaza_un_dueno_por_defecto()
    {
        var log = new EventLog();
        var ex = Assert.Throws<ArgumentException>(() =>
            log.Emit(0, EventKind.ActionStarted, default(AgentId)));
        Assert.Contains("owner", ex.Message);
        Assert.Empty(log);
    }

    [Fact]
    public void Emit_rechaza_una_accion_por_defecto()
    {
        var log = new EventLog();
        var agent = new AgentId("agente-1");
        var ex = Assert.Throws<ArgumentException>(() =>
            log.Emit(0, EventKind.ActionStarted, agent, action: default(ActionId)));
        Assert.Contains("action", ex.Message);
        Assert.Empty(log);
    }

    [Fact]
    public void Emit_rechaza_un_valor_por_defecto_en_el_payload()
    {
        var log = new EventLog();
        var agent = new AgentId("agente-1");
        var invalidData = new Dictionary<string, EventValue>
        {
            ["valido"] = EventValue.Of(123),
            ["invalido"] = default,
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            log.Emit(0, EventKind.ActionStarted, agent, data: invalidData));
        Assert.Contains("data", ex.Message);
        Assert.Empty(log);
    }

    [Fact]
    public void Emit_rechaza_un_tipo_de_evento_desconocido()
    {
        var log = new EventLog();
        var agent = new AgentId("agente-1");
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            log.Emit(0, (EventKind)999, agent));
        Assert.Empty(log);
    }

    [Fact]
    public void Emit_rechaza_un_contenido_con_un_sustituto_suelto()
    {
        var log = new EventLog();
        var agent = new AgentId("agente-1");
        var ex = Assert.Throws<ArgumentException>(() =>
            log.Emit(0, EventKind.ActionStarted, agent, content: "\ud800"));
        Assert.Contains("content", ex.Message);
        Assert.Empty(log);
    }

    [Fact]
    public void El_payload_se_copia_al_emitir()
    {
        var log = new EventLog();
        var agent = new AgentId("agente-1");
        var dict = new Dictionary<string, EventValue>
        {
            ["clave"] = EventValue.Of("inicial"),
        };

        var ev = log.Emit(0, EventKind.ActionStarted, agent, data: dict);
        var jsonAntes = ev.ToJson();

        dict["clave"] = EventValue.Of("modificado");
        dict["otra"] = EventValue.Of(999);

        Assert.Single(ev.Data);
        Assert.Equal(jsonAntes, ev.ToJson());
    }

    [Fact]
    public void Sin_contenido_no_hay_huella()
    {
        var log = new EventLog();
        var agent = new AgentId("agente-1");

        var ev = log.Emit(0, EventKind.ActionStarted, agent);

        Assert.Null(ev.Content);
        Assert.Null(ev.Digest);

        var json = ev.ToJson();
        Assert.DoesNotContain("content", json);
        Assert.DoesNotContain("digest", json);
    }
}
