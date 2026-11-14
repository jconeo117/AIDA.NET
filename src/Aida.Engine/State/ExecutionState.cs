using Aida.Engine.Randomness;

namespace Aida.Engine.State;

/// <summary>Una corrida del motor: qué agente, en qué tick y con qué semilla.</summary>
/// <remarks>
/// <para>Es <c>ExecutionState</c> de la 0.1 (<c>state.py:643-676</c>). Lleva la fuente de aleatoriedad y se pasa
/// explícitamente a quien la necesita: nunca un generador global compartido, que es donde muere la
/// reproducibilidad.</para>
/// <para>No lleva las situaciones eventuales ni calcula las activas: en la 0.1 solo las leen la persistencia y la
/// explicación, y llegan con ellas en N007. El tick lo avanza el bucle (N006, <c>loop.py:81</c>).</para>
/// </remarks>
internal sealed class ExecutionState
{
    /// <summary>Crea una corrida.</summary>
    /// <param name="agent">El agente, ya recompuesto.</param>
    /// <param name="tick">El tick actual.</param>
    /// <param name="rng">La fuente de aleatoriedad de la corrida.</param>
    internal ExecutionState(AgentState agent, long tick, RngSource rng)
    {
        Agent = agent;
        Tick = tick;
        Rng = rng;
    }

    /// <summary>El agente que delibera.</summary>
    internal AgentState Agent { get; }

    /// <summary>El tick actual. Solo lo avanza el bucle.</summary>
    internal long Tick { get; set; }

    /// <summary>La fuente de aleatoriedad.</summary>
    internal RngSource Rng { get; }

    /// <summary>La semilla de la corrida: un fallo se reproduce corriendo la misma.</summary>
    internal long Seed => Rng.Seed;
}
