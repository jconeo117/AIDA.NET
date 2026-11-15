using Aida.Engine.Ontology;

namespace Aida.Engine.Inbox;

/// <summary>El buzón de entrada: lo que llegó de fuera, en orden de llegada y con capacidad dura.</summary>
/// <remarks>
/// <para>Es <c>EventQueue</c> de la 0.1 (<c>queue.py:41-84</c>), disciplina FIFO y sin prioridades: hoy hay un solo remitente
/// posible, y una disciplina que nadie necesita es superficie muerta.</para>
/// <para>Lo que solo usa la persistencia (<c>pending</c>, <c>restore</c>, <c>next_seq</c>) llega con ella en N007.</para>
/// </remarks>
internal sealed class EventQueue
{
    private readonly Queue<IncomingEvent> _items = new();
    private long _nextSeq;

    /// <summary>Crea un buzón vacío.</summary>
    /// <param name="capacity">Cuántos eventos admite sin procesar.</param>
    internal EventQueue(long capacity) => Capacity = capacity;

    /// <summary>La capacidad declarada.</summary>
    internal long Capacity { get; }

    /// <summary>Cuántos eventos esperan.</summary>
    internal int Count => _items.Count;

    /// <summary>Encola un evento nuevo.</summary>
    /// <param name="tick">El instante en que se recibe.</param>
    /// <param name="owner">A quién va dirigido.</param>
    /// <param name="content">El contenido exacto.</param>
    /// <returns>El evento encolado, con su número de orden.</returns>
    /// <exception cref="QueueFullException">Si el buzón está lleno.</exception>
    internal IncomingEvent Push(long tick, AgentId owner, string content)
    {
        if (_items.Count >= Capacity)
        {
            throw new QueueFullException(Capacity);
        }

        var item = new IncomingEvent(tick, owner, content, _nextSeq);
        _nextSeq++;
        _items.Enqueue(item);
        return item;
    }

    /// <summary>Saca todo lo que ya venció (<c>tick &lt;= actual</c>), en orden de llegada.</summary>
    /// <param name="currentTick">El tick actual.</param>
    /// <returns>Los eventos listos, quizá ninguno.</returns>
    /// <remarks>Se detiene en el primero que aún no vence, como la 0.1 (<c>queue.py:65-75</c>): los eventos entran con tick no
    /// decreciente, así que no hace falta recorrer la cola entera.</remarks>
    internal List<IncomingEvent> PopReady(long currentTick)
    {
        var ready = new List<IncomingEvent>();
        while (_items.Count > 0 && _items.Peek().Tick <= currentTick)
        {
            ready.Add(_items.Dequeue());
        }

        return ready;
    }

    /// <summary>El tick del próximo evento sin procesar.</summary>
    /// <returns>El tick, o <c>null</c> si no hay ninguno.</returns>
    internal long? PeekTick() => _items.Count > 0 ? _items.Peek().Tick : null;
}
