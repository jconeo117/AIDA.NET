using Aida.Engine.Numerics;
using Aida.Engine.Ontology;

namespace Aida.Engine.State;

/// <summary>El hastío y el avance de la acción en curso, de <see cref="AgentState"/>.</summary>
/// <remarks>
/// Son la traducción de <c>state.py:533-593</c>. Van en su propio archivo solo por el límite de 300 líneas; es el mismo
/// tipo.
/// </remarks>
public sealed partial class AgentState
{
    /// <summary>Registra que el agente acaba de completar una acción. Una ganancia de 0 o menos no deja rastro.</summary>
    /// <param name="action">La acción completada.</param>
    /// <param name="gain">El hastío que deja, en <c>(0, 1]</c>.</param>
    internal void Saturate(ActionId action, double gain)
    {
        if (gain <= 0.0)
        {
            return;
        }

        var current = _saturation.TryGetValue(action, out var value) ? value : 0.0;
        _saturation[action] = PyFloat.Max(0.0, PyFloat.Min(1.0, current + gain));
    }

    /// <summary>Disipa el hastío a la tasa de <c>TuningSpec.saturation_recovery</c>. Lo que llega a cero se olvida.</summary>
    /// <param name="rate">La tasa de recuperación. Con 0 o menos no cambia nada.</param>
    /// <remarks>Recorre una copia de las claves, ordenadas por identificador, porque se borran mientras se recorren.</remarks>
    internal void RecoverNovelty(double rate)
    {
        if (rate <= 0.0)
        {
            return;
        }

        foreach (var action in _saturation.Keys.ToList())
        {
            var remaining = _saturation[action] - rate;
            if (remaining <= 0.0)
            {
                _saturation.Remove(action);
            }
            else
            {
                _saturation[action] = remaining;
            }
        }
    }

    /// <summary>Consume un tick de lo que el agente esté haciendo. Se detiene en 0 sin borrar la acción: quien llama
    /// necesita ver qué acaba de terminar.</summary>
    /// <remarks><see cref="PerformingAction"/> es inmutable: se reemplaza entero, con el mismo <c>TotalTicks</c>.</remarks>
    internal void Advance()
    {
        var performing = Performing;
        if (performing is null || performing.TicksRemaining == 0)
        {
            return;
        }

        Performing = new PerformingAction(performing.Action, performing.TicksRemaining - 1, performing.TotalTicks);
    }
}
