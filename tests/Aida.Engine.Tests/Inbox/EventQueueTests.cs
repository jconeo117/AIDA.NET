using Aida.Engine.Inbox;
using Aida.Engine.Ontology;

namespace Aida.Engine.Tests.Inbox;

public sealed class EventQueueTests
{
    private static readonly AgentId TestAgent = new("centinela");

    [Fact]
    public void El_buzon_lleno_revienta_con_el_mensaje_de_la_01()
    {
        var queue = new EventQueue(2);
        queue.Push(1, TestAgent, "primero");
        queue.Push(1, TestAgent, "segundo");

        var ex = Assert.Throws<QueueFullException>(() => queue.Push(1, TestAgent, "tercero"));
        Assert.Equal("cola llena: capacidad=2, no admite un evento más", ex.Message);
    }

    [Fact]
    public void PopReady_saca_lo_vencido_en_orden_y_para_en_lo_que_no_vence()
    {
        var queue = new EventQueue(10);
        queue.Push(1, TestAgent, "t1_a");
        queue.Push(1, TestAgent, "t1_b");
        queue.Push(3, TestAgent, "t3");
        queue.Push(5, TestAgent, "t5");

        var readyAt2 = queue.PopReady(2);
        Assert.Equal(2, readyAt2.Count);
        Assert.Equal("t1_a", readyAt2[0].Content);
        Assert.Equal("t1_b", readyAt2[1].Content);
        Assert.Equal(2, queue.Count);
        Assert.Equal(3, queue.PeekTick());

        var readyAt3 = queue.PopReady(3);
        Assert.Single(readyAt3);
        Assert.Equal("t3", readyAt3[0].Content);
        Assert.Equal(1, queue.Count);
        Assert.Equal(5, queue.PeekTick());

        var readyAt4 = queue.PopReady(4);
        Assert.Empty(readyAt4);
        Assert.Equal(1, queue.Count);

        var readyAt5 = queue.PopReady(5);
        Assert.Single(readyAt5);
        Assert.Equal("t5", readyAt5[0].Content);
        Assert.Equal(0, queue.Count);
        Assert.Null(queue.PeekTick());
    }

    [Fact]
    public void El_numero_de_orden_crece_aunque_se_vacie()
    {
        var queue = new EventQueue(5);
        var e0 = queue.Push(1, TestAgent, "primero");
        var e1 = queue.Push(1, TestAgent, "segundo");
        Assert.Equal(0, e0.Seq);
        Assert.Equal(1, e1.Seq);

        queue.PopReady(1);
        Assert.Equal(0, queue.Count);

        var e2 = queue.Push(2, TestAgent, "tercero");
        Assert.Equal(2, e2.Seq);
    }

    [Fact]
    public void PeekTick_de_un_buzon_vacio_es_null()
    {
        var queue = new EventQueue(5);
        Assert.Null(queue.PeekTick());

        queue.Push(10, TestAgent, "mensaje");
        Assert.Equal(10, queue.PeekTick());

        queue.PopReady(10);
        Assert.Null(queue.PeekTick());
    }
}
