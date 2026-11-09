using System.Collections.Immutable;
using System.Text;
using Aida.Engine.Ontology;
using Aida.Engine.Serialization;

namespace Aida.Engine.Events;

/// <summary>Un hecho ocurrido. Inmutable: el pasado no se reescribe.</summary>
/// <remarks>
/// Solo lo crea <see cref="EventLog.Emit"/>, que es quien numera y calcula la huella; así nadie puede
/// componer un evento con un contenido y una huella que no se correspondan (<c>engine/events.py:234-248</c>).
/// Su línea JSON (<see cref="ToJson"/>) es la que se compara byte a byte con la 0.1 (ADR 0013).
/// </remarks>
public sealed class Event
{
    internal Event(long seq, long tick, EventKind kind, AgentId owner, ActionId? action,
        ImmutableSortedDictionary<string, EventValue> data, string? content, string? digest)
    {
        Seq = seq;
        Tick = tick;
        Kind = kind;
        Owner = owner;
        Action = action;
        Data = data;
        Content = content;
        Digest = digest;
    }

    /// <summary>El número de orden en el registro: empieza en 0, crece de uno en uno y no se reutiliza.</summary>
    public long Seq { get; }

    /// <summary>El instante del motor en el que ocurrió.</summary>
    public long Tick { get; }

    /// <summary>Qué clase de hecho es.</summary>
    public EventKind Kind { get; }

    /// <summary>De quién es el hecho. Siempre hay dueño (ADR 0006).</summary>
    public AgentId Owner { get; }

    /// <summary>La acción a la que se refiere, si se refiere a una.</summary>
    public ActionId? Action { get; }

    /// <summary>El payload, ordenado por punto de código. Vacío si el hecho no lleva ninguno.</summary>
    public ImmutableSortedDictionary<string, EventValue> Data { get; }

    /// <summary>El contenido exacto de una entrada de fuera, sin resumir ni normalizar; <see langword="null"/> en el resto.</summary>
    public string? Content { get; }

    /// <summary>La huella BLAKE2b de 16 bytes de <see cref="Content"/>, en hexadecimal; <see langword="null"/> si no hay contenido.</summary>
    public string? Digest { get; }

    /// <summary>La línea JSON canónica del evento, sin salto de línea final.</summary>
    /// <returns>La línea, idéntica a la de <c>Event.to_json</c> de la 0.1.</returns>
    /// <remarks>Se omite lo que no lleva valor, incluido el payload vacío (<c>engine/events.py:258-299</c>).</remarks>
    public string ToJson()
    {
        var builder = new StringBuilder();
        builder.Append("{\"seq\":").Append(CanonicalJson.FormatInteger(Seq));
        builder.Append(",\"tick\":").Append(CanonicalJson.FormatInteger(Tick));
        builder.Append(",\"kind\":");
        CanonicalJson.WriteString(builder, EventKindNames.ToWire(Kind));
        builder.Append(",\"owner\":");
        CanonicalJson.WriteString(builder, Owner.Value);
        if (Action is { } action)
        {
            builder.Append(",\"action\":");
            CanonicalJson.WriteString(builder, action.Value);
        }

        if (Data.Count > 0)
        {
            builder.Append(",\"data\":{");
            var first = true;
            foreach (var (key, value) in Data)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                CanonicalJson.WriteString(builder, key);
                builder.Append(':');
                value.WriteJson(builder);
            }

            builder.Append('}');
        }

        if (Content is not null)
        {
            builder.Append(",\"content\":");
            CanonicalJson.WriteString(builder, Content);
        }

        if (Digest is not null)
        {
            builder.Append(",\"digest\":");
            CanonicalJson.WriteString(builder, Digest);
        }

        builder.Append('}');
        return builder.ToString();
    }
}
