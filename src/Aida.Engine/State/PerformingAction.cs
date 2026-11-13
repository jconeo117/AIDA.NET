using Aida.Engine.Ontology;

namespace Aida.Engine.State;

/// <summary>Lo que el agente está haciendo ahora mismo, con los ticks que le quedan.</summary>
/// <remarks>
/// Inmutable: avanzar crea otro con <c>TicksRemaining</c> una unidad menor (<c>engine/state.py:54-85</c>). No lleva
/// destinatario: no existe otro agente al que apuntar, así que el <c>target</c> de la versión de la que nace no tiene
/// sentido aquí.
/// </remarks>
public sealed class PerformingAction
{
    /// <summary>Crea la acción en curso.</summary>
    /// <param name="action">La acción.</param>
    /// <param name="ticksRemaining">Lo que queda. En 0, la acción ya terminó y falta que alguien lo note.</param>
    /// <param name="totalTicks">Lo que se planificó al empezar. De aquí sale lo invertido: <c>totalTicks - ticksRemaining</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="ticksRemaining"/> o <paramref name="totalTicks"/> son negativos.</exception>
    internal PerformingAction(ActionId action, long ticksRemaining, long totalTicks = 0)
    {
        if (ticksRemaining < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticksRemaining), ticksRemaining, $"ticks_remaining = {ticksRemaining}, no puede ser negativo");
        }

        if (totalTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTicks), totalTicks, $"total_ticks = {totalTicks}, no puede ser negativo");
        }

        Action = action;
        TicksRemaining = ticksRemaining;
        TotalTicks = totalTicks;
    }

    /// <summary>La acción que se está haciendo.</summary>
    public ActionId Action { get; }

    /// <summary>Los ticks que quedan. En 0, la acción ha terminado y aún no se ha decidido qué sigue.</summary>
    public long TicksRemaining { get; }

    /// <summary>Los ticks planificados al empezar. El coste de cambiar de contexto crece con lo que ya se ha invertido.</summary>
    public long TotalTicks { get; }
}
