using Aida.Engine.Authorization;
using Aida.Engine.Deliberation;
using Aida.Engine.Events;
using Aida.Engine.Inbox;
using Aida.Engine.Ontology;
using Aida.Engine.Packs;
using Aida.Engine.Ports;
using Aida.Engine.Randomness;
using Aida.Engine.State;

namespace Aida.Engine.Loop;

/// <summary>Un agente corriendo, tick a tick, con su registro, su buzón y su techo de autoridad.</summary>
/// <remarks>
/// <para>Es <c>Loop</c> de la 0.1 (<c>loop.py:34-238</c>). El orden de un paso no es arbitrario: decaer antes de decidir, para
/// que todo se decida sobre el mismo instante, y recomponer con el desgaste ya fresco, para deliberar con el temperamento de
/// ahora. Es el orden que reproducen los registros dorados, byte a byte (ADR 0027).</para>
/// <para>Síncrono (ADR 0004): no sabe cuándo despertar. Lo mueve quien lo hospeda, que en N006 son los tests y en la
/// plataforma será el servicio con el reloj híbrido.</para>
/// </remarks>
internal sealed class AgentLoop
{
    private readonly IIntentClassifier? _intent;

    private AgentLoop(ContentPack pack, ExecutionState execution, EventQueue inbox, IIntentClassifier? intent)
    {
        Pack = pack;
        Execution = execution;
        Inbox = inbox;
        Authority = new Authority(pack.Authority);
        _intent = intent;
    }

    /// <summary>El pack con el que corre.</summary>
    internal ContentPack Pack { get; }

    /// <summary>La corrida: agente, tick y semilla.</summary>
    internal ExecutionState Execution { get; }

    /// <summary>El registro de solo añadir.</summary>
    internal EventLog Log { get; } = new();

    /// <summary>El buzón de entrada.</summary>
    internal EventQueue Inbox { get; }

    /// <summary>El techo de autoridad y las propuestas vivas.</summary>
    internal Authority Authority { get; }

    /// <summary>Arranca un agente nuevo a partir de lo que el pack declaró para él (<c>loop.py:52-73</c>).</summary>
    /// <param name="pack">El pack.</param>
    /// <param name="spec">El agente del pack.</param>
    /// <param name="seed">La semilla de la corrida.</param>
    /// <param name="inboxCapacity">La capacidad del buzón.</param>
    /// <param name="intent">El clasificador de intención, o <c>null</c> si no hay: el bucle no construye uno por su cuenta.</param>
    /// <returns>El bucle, en el tick 0.</returns>
    internal static AgentLoop Start(ContentPack pack, AgentSpec spec, long seed, long inboxCapacity, IIntentClassifier? intent = null)
    {
        var agent = AgentState.FromSpec(pack, spec);
        var execution = new ExecutionState(agent, 0, new RngSource(seed));
        return new AgentLoop(pack, execution, new EventQueue(inboxCapacity), intent);
    }

    /// <summary>Un paso del bucle (<c>loop.py:79-156</c>).</summary>
    internal void Step()
    {
        Execution.Tick++;
        long tick = Execution.Tick;
        var agent = Execution.Agent;
        var owner = agent.AgentId;

        NeedId? requested = null;
        foreach (var item in Inbox.PopReady(tick))
        {
            Log.Emit(tick, EventKind.InputObserved, owner, content: item.Content);
            if (_intent is null)
            {
                continue;
            }

            var intentId = _intent.Classify(item.Content);
            IntentSpec? matched = null;
            if (intentId is not null && Pack.IntentById.TryGetValue(new IntentId(intentId), out var found))
            {
                matched = found;
            }

            if (matched is not null)
            {
                requested = matched.Need;
                Log.Emit(tick, EventKind.IntentClassified, owner,
                    data: new Dictionary<string, EventValue> { ["intent"] = EventValue.Of(matched.Id.Value) });
            }
            else
            {
                Log.Emit(tick, EventKind.IntentClassified, owner);
            }
        }

        foreach (var expired in Authority.Expired(tick))
        {
            Log.Emit(tick, EventKind.ActionExpired, owner, action: expired);
        }

        agent.Decay(Pack);
        agent.AdvanceFloorStreak(Pack, Log, tick);
        agent.RecoverNovelty(Pack.Tuning.SaturationRecovery);
        agent.RefreshState(Pack);
        agent.Recompose(Pack, []);

        NeedId? forced = requested;
        var performing = agent.Performing;
        if (performing is not null)
        {
            agent.Advance();
            performing = agent.Performing;
            if (performing is not null && performing.TicksRemaining == 0)
            {
                Finish(performing);
            }
            else if (performing is not null && Pack.ActionById[performing.Action].Interruptible)
            {
                var urgent = CriticalNeeds.Worst(Execution, Pack);
                if (urgent is not null)
                {
                    Abort(performing, urgent.Value);
                    forced = urgent;
                }
            }
        }

        if (agent.Performing is null)
        {
            // El aborto de arriba ya hace ganar a la emergencia cuando había algo en curso; esto cubre al agente parado.
            if (requested is not null && forced == requested)
            {
                var urgent = CriticalNeeds.Worst(Execution, Pack);
                if (urgent is not null)
                {
                    forced = urgent;
                }
            }

            Begin(forced);
        }
    }

    /// <summary>Concede una ejecución de lo propuesto (<c>loop.py:158-168</c>).</summary>
    /// <param name="action">La acción.</param>
    /// <returns><c>false</c> si no había propuesta viva.</returns>
    internal bool Approve(ActionId action)
    {
        if (!Authority.Approve(action))
        {
            return false;
        }

        Log.Emit(Execution.Tick, EventKind.ActionAuthorized, Execution.Agent.AgentId, action: action);
        return true;
    }

    /// <summary>Retira lo propuesto sin conceder nada (<c>loop.py:170-180</c>).</summary>
    /// <param name="action">La acción.</param>
    /// <returns><c>false</c> si no había propuesta viva.</returns>
    internal bool Deny(ActionId action)
    {
        if (!Authority.Deny(action))
        {
            return false;
        }

        Log.Emit(Execution.Tick, EventKind.ActionDenied, Execution.Agent.AgentId, action: action);
        return true;
    }

    // loop.py:182-219. Lo que está por encima del techo se propone y se descarta, y se vuelve a deliberar con lo que sí puede
    // hacer solo: el techo no deja al asistente parado esperando.
    private void Begin(NeedId? mustAddress)
    {
        var agent = Execution.Agent;
        var vetoed = new HashSet<ActionId>();
        ActionSpec action;
        while (true)
        {
            var decision = Deliberator.Decide(Execution, Pack, mustAddress, vetoed);
            if (decision is null)
            {
                return;
            }

            action = Pack.ActionById[decision.Action];
            if (Authority.MayStart(action))
            {
                break;
            }

            if (Authority.Propose(action, Execution.Tick))
            {
                Log.Emit(Execution.Tick, EventKind.ActionProposed, agent.AgentId, action: action.Id,
                    data: new Dictionary<string, EventValue> { ["expires_at"] = EventValue.Of(Authority.Pending[action.Id]) });
            }

            vetoed.Add(action.Id);
        }

        agent.Performing = new PerformingAction(action.Id, action.DurationTicks, action.DurationTicks);
        Log.Emit(Execution.Tick, EventKind.ActionStarted, agent.AgentId, action: action.Id);
    }

    // loop.py:221-232.
    private void Finish(PerformingAction performing)
    {
        var agent = Execution.Agent;
        var action = Pack.ActionById[performing.Action];
        agent.Apply(action.Effects, Pack);
        agent.Saturate(action.Id, action.SaturationGain);
        agent.Performing = null;
        Log.Emit(Execution.Tick, EventKind.ActionCompleted, agent.AgentId, action: action.Id);
    }

    // loop.py:234-243.
    private void Abort(PerformingAction performing, NeedId urgent)
    {
        var agent = Execution.Agent;
        Log.Emit(Execution.Tick, EventKind.ActionAborted, agent.AgentId, action: performing.Action,
            data: new Dictionary<string, EventValue> { ["need"] = EventValue.Of(urgent.Value) });
        agent.Performing = null;
    }
}
