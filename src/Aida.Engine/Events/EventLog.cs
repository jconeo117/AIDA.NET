using System.Collections;
using System.Collections.Immutable;
using System.Text;
using Aida.Engine.Ontology;
using Aida.Engine.Serialization;

namespace Aida.Engine.Events;

/// <summary>El registro de solo añadir de todo lo que ocurre en el motor.</summary>
/// <remarks>
/// Nada cambia el estado sin emitir un evento (ADR 0005). <see cref="Emit"/> es la única puerta: numera
/// con <see cref="Event.Seq"/> y calcula la huella del contenido. La ventana, el vertido a archivo y las
/// consultas de la 0.1 se portan cuando haya quien los use (contrato N002, §1.4).
/// </remarks>
public sealed class EventLog : IReadOnlyCollection<Event>
{
    private static readonly ImmutableSortedDictionary<string, EventValue> _emptyData =
        ImmutableSortedDictionary.Create<string, EventValue>(CodePointComparer.Instance);

    private readonly List<Event> _events = [];

    /// <summary>Registra un hecho y devuelve el evento ya numerado.</summary>
    /// <param name="tick">El instante del motor.</param>
    /// <param name="kind">Qué clase de hecho es.</param>
    /// <param name="owner">De quién es. No puede ser el valor por defecto.</param>
    /// <param name="action">La acción a la que se refiere, si se refiere a una.</param>
    /// <param name="data">El payload. Se copia: cambiar el diccionario después no cambia el evento.</param>
    /// <param name="content">El contenido exacto de una entrada de fuera. Si se pasa, se calcula su huella aquí.</param>
    /// <returns>El evento, con <see cref="Event.Seq"/> igual al número de eventos registrados antes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="kind"/> no es un valor de <see cref="EventKind"/>.</exception>
    /// <exception cref="ArgumentException">Si <paramref name="owner"/> o <paramref name="action"/> son el valor por defecto, si
    /// un valor de <paramref name="data"/> es el valor por defecto, o si <paramref name="content"/> no se puede codificar en UTF-8.</exception>
    /// <remarks>La huella no es un parámetro: quien llama no puede componerla por su cuenta (<c>engine/events.py:384-389</c>).</remarks>
    public Event Emit(long tick, EventKind kind, AgentId owner, ActionId? action = null,
        IReadOnlyDictionary<string, EventValue>? data = null, string? content = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Tipo de evento desconocido.");
        }

        if (owner.Value is null)
        {
            throw new ArgumentException("Todo hecho tiene dueño: owner no puede ser el valor por defecto.", nameof(owner));
        }

        if (action is { Value: null })
        {
            throw new ArgumentException("La acción, si se pasa, no puede ser el valor por defecto.", nameof(action));
        }

        if (data is not null && data.Values.Any(value => value.Kind == EventValueKind.None))
        {
            throw new ArgumentException("Un valor del payload es el valor por defecto.", nameof(data));
        }

        var digest = content is null ? null : DigestOf(content);
        var payload = data is null || data.Count == 0
            ? _emptyData
            : ImmutableSortedDictionary.CreateRange(CodePointComparer.Instance, data);
        var recorded = new Event(_events.Count, tick, kind, owner, action, payload, content, digest);
        _events.Add(recorded);
        return recorded;
    }

    /// <summary>Cuántos eventos hay registrados.</summary>
    public int Count => _events.Count;

    /// <summary>Recorre los eventos en orden de <see cref="Event.Seq"/>.</summary>
    /// <returns>El enumerador.</returns>
    public IEnumerator<Event> GetEnumerator() => _events.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static string DigestOf(string content)
    {
        try
        {
            return ContentDigest.Of(content);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException("El contenido tiene sustitutos UTF-16 sueltos y no es UTF-8 válido.", nameof(content), exception);
        }
    }
}
