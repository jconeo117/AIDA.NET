using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Aida.Engine.Disposition;
using Aida.Engine.Numerics;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;
using Aida.Engine.Serialization;

namespace Aida.Engine.State;

/// <summary>Cómo está un agente ahora mismo y qué deriva de su personalidad.</summary>
/// <remarks>
/// <para>Su API pública es de solo lectura: lo muta el motor con métodos <c>internal</c>, y solo el bucle los llama
/// (ADR 0025). La evolución derivada (decaimiento, rachas, hastío, desgaste, recomposición) no emite eventos porque se
/// reproduce con el pack, la semilla y el registro. La escalada de una necesidad sí lo emite.</para>
/// <para>Todos los mapas se recorren ordenados por identificador (punto de código), que es el <c>sorted()</c> de la 0.1:
/// el producto y la suma en coma flotante no son asociativos (<c>disposition.py:10-18</c>).</para>
/// </remarks>
public sealed partial class AgentState
{
    private readonly SortedDictionary<NeedId, double> _needs = new(IdOrder.Instance);
    private readonly SortedDictionary<NeedId, double> _debt = new(IdOrder.Instance);
    private readonly SortedDictionary<NeedId, long> _floorStreak = new(IdOrder.Instance);
    private readonly SortedDictionary<TraitId, double> _traits = new(IdOrder.Instance);
    private readonly SortedDictionary<GoalId, GoalStatus> _goals = new(IdOrder.Instance);
    private readonly SortedDictionary<ActionId, double> _saturation = new(IdOrder.Instance);
    private SortedDictionary<NeedId, double> _caps = new(IdOrder.Instance);

    private AgentState(AgentId agentId, SortedDictionary<NeedId, double> needs, SortedDictionary<TraitId, double> traits,
        SortedDictionary<GoalId, GoalStatus> goals, SortedDictionary<string, double> tastes)
    {
        AgentId = agentId;
        _needs = needs;
        _traits = traits;
        _goals = goals;
        Needs = new ReadOnlyDictionary<NeedId, double>(_needs);
        Debt = new ReadOnlyDictionary<NeedId, double>(_debt);
        FloorStreak = new ReadOnlyDictionary<NeedId, long>(_floorStreak);
        Traits = new ReadOnlyDictionary<TraitId, double>(_traits);
        Goals = new ReadOnlyDictionary<GoalId, GoalStatus>(_goals);
        Saturation = new ReadOnlyDictionary<ActionId, double>(_saturation);
        Tastes = new ReadOnlyDictionary<string, double>(tastes);
        NeedWeights = new ReadOnlyDictionary<NeedId, double>(new SortedDictionary<NeedId, double>(IdOrder.Instance));
        Gains = new ReadOnlyDictionary<NeedId, double>(new SortedDictionary<NeedId, double>(IdOrder.Instance));
        Costs = new ReadOnlyDictionary<NeedId, double>(new SortedDictionary<NeedId, double>(IdOrder.Instance));
        Caps = new ReadOnlyDictionary<NeedId, double>(_caps);
    }

    /// <summary>De quién es este estado.</summary>
    public AgentId AgentId { get; }

    /// <summary>El valor de cada necesidad, en <c>[0, techo]</c>. Ningún lector ve la deuda aquí.</summary>
    public IReadOnlyDictionary<NeedId, double> Needs { get; }

    /// <summary>Lo que el suelo se ha comido de cada eje. Siempre mayor que 0; solo están los ejes con deuda.</summary>
    public IReadOnlyDictionary<NeedId, double> Debt { get; }

    /// <summary>Ticks seguidos en el suelo de cada eje que puede escalar.</summary>
    public IReadOnlyDictionary<NeedId, long> FloorStreak { get; }

    /// <summary>La intensidad de cada rasgo de este agente, en <c>[-1, 1]</c>.</summary>
    public IReadOnlyDictionary<TraitId, double> Traits { get; }

    /// <summary>En qué anda cada meta del agente.</summary>
    public IReadOnlyDictionary<GoalId, GoalStatus> Goals { get; }

    /// <summary>El hastío por acción, en <c>(0, 1]</c>. Lo que llega a cero desaparece.</summary>
    public IReadOnlyDictionary<ActionId, double> Saturation { get; }

    /// <summary>Lo que el agente está haciendo, o <see langword="null"/> si está ocioso.</summary>
    public PerformingAction? Performing { get; internal set; }

    /// <summary>El desgaste, en <c>[0, 1]</c>. Derivado de las necesidades; lo recalcula el motor cada tick.</summary>
    public double Strain { get; private set; }

    /// <summary>Cuánto le importa a este agente cada eje. Derivado de rasgos y metas activas.</summary>
    public IReadOnlyDictionary<NeedId, double> NeedWeights { get; private set; }

    /// <summary>Su aptitud en cada eje: cuánto le rinde lo que repone.</summary>
    public IReadOnlyDictionary<NeedId, double> Gains { get; private set; }

    /// <summary>Su temple en cada eje: cuánto le cuesta lo que gasta.</summary>
    public IReadOnlyDictionary<NeedId, double> Costs { get; private set; }

    /// <summary>Su techo efectivo en cada eje, nunca mayor que 1.</summary>
    public IReadOnlyDictionary<NeedId, double> Caps { get; private set; }

    /// <summary>Su predilección (mayor que 1) o aversión (menor que 1) hacia un objetivo, por su id en texto.</summary>
    public IReadOnlyDictionary<string, double> Tastes { get; }

    /// <summary>Sus parámetros de decisión ya compuestos.</summary>
    public Temperament Temperament { get; private set; } = null!;

    /// <summary>El estado inicial de un agente del pack. Es la única forma de construir uno.</summary>
    /// <param name="pack">El pack del que sale.</param>
    /// <param name="spec">Las condiciones de partida del agente.</param>
    /// <returns>El agente, ya recompuesto.</returns>
    /// <exception cref="KeyNotFoundException">Si un rasgo o una meta no están en el pack; las comprobaciones de N003 lo impiden antes.</exception>
    /// <remarks>
    /// Las metas nacen bloqueadas si dependen de otra y activas si no (<c>state.py:248-289</c>). Termina recomponiendo: la
    /// misma puerta que usa cualquier cambio posterior, para no tener dos fórmulas de arranque.
    /// </remarks>
    public static AgentState FromSpec(ContentPack pack, AgentSpec spec)
    {
        var needs = new SortedDictionary<NeedId, double>(IdOrder.Instance);
        foreach (var need in pack.Needs)
        {
            needs[need.Id] = need.Initial;
        }

        foreach (var (needId, value) in spec.InitialNeeds)
        {
            needs[needId] = value;
        }

        var traits = new SortedDictionary<TraitId, double>(IdOrder.Instance);
        foreach (var (traitId, value) in spec.Traits)
        {
            traits[traitId] = value;
        }

        var goals = new SortedDictionary<GoalId, GoalStatus>(IdOrder.Instance);
        foreach (var goal in spec.Goals)
        {
            goals[goal.Id] = goal.RequiresGoal is null ? GoalStatus.Active : GoalStatus.Locked;
        }

        var agent = new AgentState(spec.Id, needs, traits, goals, TasteComposition.Compose(spec.Tastes));
        agent.Recompose(pack, []);
        return agent;
    }

    /// <summary>El techo efectivo de un eje, o el nominal de <paramref name="need"/> si aún no se ha compuesto.</summary>
    /// <param name="need">La definición del eje.</param>
    /// <returns>El techo efectivo.</returns>
    public double CapOf(NeedSpec need) => _caps.TryGetValue(need.Id, out var cap) ? cap : need.Cap;

    /// <summary>El techo efectivo de un eje, o 1.0 si aún no se ha compuesto.</summary>
    /// <param name="need">El identificador del eje.</param>
    /// <returns>El techo efectivo.</returns>
    public double CapOf(NeedId need) => _caps.TryGetValue(need, out var cap) ? cap : 1.0;

    /// <summary>Cuánto apremia este eje ahora mismo, para este agente. Ciega a la deuda a propósito.</summary>
    /// <param name="need">La definición del eje.</param>
    /// <returns>La urgencia, en <c>[0, 1]</c>.</returns>
    /// <remarks>Si la deuda se filtrara aquí, habría dos formas de expresar «esto va mal»: el valor bajo y la deuda alta.</remarks>
    public double Urgency(NeedSpec need) => Pressure.Urgency(_needs[need.Id], need.UrgencyExponent, CapOf(need));

    /// <summary>Las metas activas, ordenadas por identificador.</summary>
    /// <returns>Los identificadores de las metas en estado activo.</returns>
    public ImmutableArray<GoalId> Pursuing() =>
        _goals.Where(entry => entry.Value == GoalStatus.Active).Select(entry => entry.Key).ToImmutableArray();

    // Rehace todo lo derivado de rasgos y del estado actual. Puerta única de recomposición (state.py:597-618).
    internal void Recompose(ContentPack pack, IEnumerable<SituationId> activeSituations)
    {
        var activeGoals = Pursuing()
            .Where(id => pack.GoalById.ContainsKey(id))
            .Select(id => pack.GoalById[id])
            .ToList();
        var traits = Traits;
        NeedWeights = new ReadOnlyDictionary<NeedId, double>(NeedComposition.ComposeNeedWeights(pack, traits, activeGoals));
        Gains = new ReadOnlyDictionary<NeedId, double>(NeedComposition.ComposeGains(pack, traits));
        Costs = new ReadOnlyDictionary<NeedId, double>(NeedComposition.ComposeCosts(pack, traits));
        _caps = NeedComposition.ComposeCaps(pack, traits);
        Caps = new ReadOnlyDictionary<NeedId, double>(_caps);
        Temperament = TemperamentComposition.Compose(pack, traits, Strain, activeSituations);
    }

    // El desgaste se deriva de las necesidades y de los techos efectivos (state.py:482-492).
    internal void RefreshState(ContentPack pack) => Strain = Pressure.ComputeStrain(pack, Needs, Caps);

    internal void Decay(ContentPack pack) => NeedDynamics.Decay(this, pack);

    internal void Apply(DeclaredMap<NeedId, double> effects, ContentPack pack) => NeedDynamics.Apply(this, effects, pack);

    internal double Restore(NeedId needId, double amount, NeedSpec need) => NeedDynamics.Restore(this, needId, amount, need);

    internal void AdvanceFloorStreak(ContentPack pack, Events.EventLog log, long tick) =>
        NeedDynamics.AdvanceFloorStreak(this, pack, log, tick);

    internal SortedDictionary<NeedId, double> MutableNeeds => _needs;

    internal SortedDictionary<NeedId, double> MutableDebt => _debt;

    internal SortedDictionary<NeedId, long> MutableFloorStreak => _floorStreak;
}
