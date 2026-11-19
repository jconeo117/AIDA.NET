using System.Collections.ObjectModel;
using Aida.Engine.Ontology;

namespace Aida.Engine.Authorization;

/// <summary>Hasta dónde llega el asistente por su cuenta, y qué pasa con lo demás: el techo de autoridad y las propuestas
/// vivas.</summary>
/// <remarks>
/// <para>Es <c>Authority</c> de la 0.1 (<c>authority.py:24-107</c>), la puerta dura del ADR 0007. No puntúa: dice sí o no. Lo
/// que queda por encima del techo del pack no se prohíbe, se propone; la propuesta caduca si nadie contesta, porque una
/// propuesta eterna acaba siendo una autorización que nadie dio.</para>
/// <para>El permiso entra solo por <see cref="Approve"/> y <see cref="Deny"/>, nunca leyendo el buzón: si un texto pudiera
/// autorizar, el techo lo pondría quien escribiera el texto. Una autorización es de un solo uso.</para>
/// </remarks>
internal sealed class Authority
{
    private readonly SortedDictionary<ActionId, long> _pending = new(IdOrder.Instance);
    private readonly HashSet<ActionId> _approved = [];

    /// <summary>Crea la autoridad con el techo del pack.</summary>
    /// <param name="spec">El techo y la caducidad de las propuestas.</param>
    internal Authority(AuthoritySpec spec)
    {
        Spec = spec;
        Pending = new ReadOnlyDictionary<ActionId, long>(_pending);
    }

    /// <summary>El techo y la caducidad que declaró el pack.</summary>
    internal AuthoritySpec Spec { get; }

    /// <summary>Las propuestas vivas: acción y tick en que caduca, en orden de identificador.</summary>
    internal IReadOnlyDictionary<ActionId, long> Pending { get; }

    /// <summary>Si empezar la acción exige un permiso que el pack no concede de antemano.</summary>
    /// <param name="action">La acción.</param>
    /// <returns><c>true</c> si su reversibilidad está por encima del techo.</returns>
    internal bool IsGated(ActionSpec action) => ReversibilityRank.Exceeds(action.Reversibility, Spec.AutonomousUpTo);

    /// <summary>Si la acción puede empezar ya. Consume la autorización si la había.</summary>
    /// <param name="action">La acción.</param>
    /// <returns><c>true</c> si no necesita permiso o si había uno concedido.</returns>
    /// <remarks>Consumir aquí y no en quien llama es deliberado: si no, una segunda ejecución con el mismo permiso
    /// dependería de que todos los llamantes se acordaran de gastarlo (<c>authority.py:54-67</c>).</remarks>
    internal bool MayStart(ActionSpec action)
    {
        if (!IsGated(action))
        {
            return true;
        }

        if (_approved.Remove(action.Id))
        {
            _pending.Remove(action.Id);
            return true;
        }

        return false;
    }

    /// <summary>Registra la propuesta de una acción.</summary>
    /// <param name="action">La acción.</param>
    /// <param name="tick">El tick actual.</param>
    /// <returns><c>false</c> si ya había una viva para esa acción: no se renueva, para que no caduque nunca.</returns>
    internal bool Propose(ActionSpec action, long tick)
    {
        if (_pending.ContainsKey(action.Id))
        {
            return false;
        }

        _pending[action.Id] = tick + Spec.ConfirmationTtlTicks;
        return true;
    }

    /// <summary>Concede una ejecución de lo propuesto.</summary>
    /// <param name="action">La acción.</param>
    /// <returns><c>false</c> si no había propuesta viva: no se autoriza lo que nadie propuso.</returns>
    internal bool Approve(ActionId action)
    {
        if (!_pending.Remove(action))
        {
            return false;
        }

        _approved.Add(action);
        return true;
    }

    /// <summary>Retira lo propuesto sin conceder nada.</summary>
    /// <param name="action">La acción.</param>
    /// <returns><c>false</c> si no había propuesta viva.</returns>
    internal bool Deny(ActionId action) => _pending.Remove(action);

    /// <summary>Saca las propuestas que caducan en este tick.</summary>
    /// <param name="tick">El tick actual.</param>
    /// <returns>Las acciones caducadas, en orden de identificador: cada una emite un evento y el registro tiene que salir
    /// igual en dos corridas.</returns>
    internal List<ActionId> Expired(long tick)
    {
        var expired = _pending.Where(entry => entry.Value <= tick).Select(entry => entry.Key).ToList();
        foreach (var action in expired)
        {
            _pending.Remove(action);
        }

        return expired;
    }
}
