using Aida.Engine.Events;
using Aida.Engine.Numerics;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;
using Aida.Engine.Serialization;

namespace Aida.Engine.State;

/// <summary>La evolución de las necesidades de un agente: decaimiento, gasto, reposición, deuda y racha en el suelo.</summary>
/// <remarks>
/// Opera sobre los mapas mutables de <see cref="AgentState"/> y solo lo llaman sus métodos internos. Es la única
/// traducción de <c>state.py:325-441</c> y <c>494-529</c>: si dos sitios calcularan el gasto, podrían divergir. La
/// evolución que cambia el estado sin emitir evento (decaimiento, gasto, reposición) se reproduce con el pack, la
/// semilla y el registro (ADR 0025); solo la escalada emite.
/// </remarks>
internal static class NeedDynamics
{
    /// <summary>Acota un valor a <c>[0, cap]</c>. Es el único sitio donde se fuerza el rango de un eje.</summary>
    /// <param name="value">El valor.</param>
    /// <param name="cap">El techo efectivo del eje.</param>
    /// <returns>El valor acotado.</returns>
    internal static double Clamp(double value, double cap) => PyFloat.Max(0.0, PyFloat.Min(cap, value));

    /// <summary>Baja un eje, y anota como deuda lo que el suelo se habría comido, con tope <c>max_debt</c>.</summary>
    /// <param name="agent">El agente.</param>
    /// <param name="amount">Cuánto baja el eje.</param>
    /// <param name="need">La definición del eje en el pack.</param>
    /// <remarks>El eje sigue viviendo en <c>[0, techo]</c>: quien lee las necesidades no se entera de la deuda.</remarks>
    internal static void Spend(AgentState agent, double amount, NeedSpec need)
    {
        var raw = agent.MutableNeeds[need.Id] - amount;
        if (raw < 0.0 && need.MaxDebt > 0.0)
        {
            agent.MutableDebt[need.Id] = PyFloat.Min(need.MaxDebt, MapOr(agent.MutableDebt, need.Id, 0.0) - raw);
        }

        agent.MutableNeeds[need.Id] = Clamp(raw, agent.CapOf(need));
    }

    /// <summary>Repone un eje, saldando primero la deuda. Devuelve lo que de verdad llegó al eje.</summary>
    /// <param name="agent">El agente.</param>
    /// <param name="needId">El eje.</param>
    /// <param name="amount">Cuánto se repone. Negativo lanza excepción: no es un gasto.</param>
    /// <param name="need">La definición del eje en el pack.</param>
    /// <returns>Lo que subió el eje, que puede ser menos de <paramref name="amount"/> si había deuda.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="amount"/> es negativo.</exception>
    internal static double Restore(AgentState agent, NeedId needId, double amount, NeedSpec need)
    {
        if (amount < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, $"amount = {PythonText.Float(amount)}, restore no admite cantidades negativas");
        }

        var owed = MapOr(agent.MutableDebt, needId, 0.0);
        if (owed > 0.0)
        {
            var paid = PyFloat.Min(owed, amount);
            amount -= paid;
            var remaining = owed - paid;
            if (remaining <= 0.0)
            {
                agent.MutableDebt.Remove(needId);
            }
            else
            {
                agent.MutableDebt[needId] = remaining;
            }
        }

        if (amount <= 0.0)
        {
            return 0.0;
        }

        var before = agent.MutableNeeds[needId];
        agent.MutableNeeds[needId] = Clamp(before + amount, agent.CapOf(need));
        return agent.MutableNeeds[needId] - before;
    }

    /// <summary>Baja cada eje según su propia tasa, en orden de identificador.</summary>
    /// <param name="agent">El agente.</param>
    /// <param name="pack">El pack.</param>
    internal static void Decay(AgentState agent, ContentPack pack)
    {
        foreach (var needId in pack.NeedById.Keys.OrderBy(n => n, IdOrder.Instance))
        {
            var need = pack.NeedById[needId];
            Spend(agent, need.DecayPerTick, need);
        }
    }

    /// <summary>Aplica los efectos de algo completado: lo positivo se repone escalado por aptitud, lo negativo se gasta
    /// escalado por temple. En orden de identificador.</summary>
    /// <param name="agent">El agente.</param>
    /// <param name="effects">Los deltas de la acción.</param>
    /// <param name="pack">El pack.</param>
    internal static void Apply(AgentState agent, DeclaredMap<NeedId, double> effects, ContentPack pack)
    {
        foreach (var needId in effects.Keys.OrderBy(n => n, IdOrder.Instance))
        {
            var delta = effects[needId];
            var need = pack.NeedById[needId];
            if (delta >= 0.0)
            {
                Restore(agent, needId, delta * ReadOnlyOr(agent.Gains, needId, 1.0), need);
            }
            else
            {
                Spend(agent, -delta * ReadOnlyOr(agent.Costs, needId, 1.0), need);
            }
        }
    }

    /// <summary>Al final de un tick: cuenta los ticks seguidos en el suelo, borra la racha del que se levanta, y emite
    /// <see cref="EventKind.NeedEscalated"/> exactamente cuando una racha alcanza <c>escalates_after</c>.</summary>
    /// <param name="agent">El agente.</param>
    /// <param name="pack">El pack.</param>
    /// <param name="log">El registro donde se emite el evento.</param>
    /// <param name="tick">El tick actual.</param>
    internal static void AdvanceFloorStreak(AgentState agent, ContentPack pack, EventLog log, long tick)
    {
        foreach (var needId in pack.NeedById.Keys.OrderBy(n => n, IdOrder.Instance))
        {
            var need = pack.NeedById[needId];
            if (need.EscalatesAfter <= 0)
            {
                continue;
            }

            if (agent.MutableNeeds[needId] > 0.0)
            {
                agent.MutableFloorStreak.Remove(needId);
                continue;
            }

            var streak = MapOr(agent.MutableFloorStreak, needId, 0L) + 1;
            agent.MutableFloorStreak[needId] = streak;
            if (streak == need.EscalatesAfter)
            {
                log.Emit(tick, EventKind.NeedEscalated, agent.AgentId,
                    data: new Dictionary<string, EventValue> { ["need"] = EventValue.Of(needId.Value) });
            }
        }
    }

    // SortedDictionary no tiene GetValueOrDefault (solo los IReadOnlyDictionary lo tienen): este es el equivalente de dict.get(k, x).
    private static TValue MapOr<TKey, TValue>(IDictionary<TKey, TValue> map, TKey key, TValue fallback) where TKey : notnull =>
        map.TryGetValue(key, out var value) ? value : fallback;

    private static TValue ReadOnlyOr<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> map, TKey key, TValue fallback) where TKey : notnull =>
        map.TryGetValue(key, out var value) ? value : fallback;
}
