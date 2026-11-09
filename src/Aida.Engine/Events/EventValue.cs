using System.Text;
using Aida.Engine.Serialization;

namespace Aida.Engine.Events;

/// <summary>Un valor escalar del payload de un evento: texto, entero, decimal o booleano.</summary>
/// <remarks>
/// El payload es plano a propósito: se serializa sin ceremonia y se resume bien para un modelo pequeño
/// (<c>engine/events.py:42-45</c>). Se distingue el entero del decimal porque se escriben distinto
/// (<c>1</c> frente a <c>1.0</c>), y la línea tiene que coincidir con la de la 0.1. El valor por defecto
/// no es válido: <see cref="EventLog.Emit"/> lo rechaza.
/// </remarks>
public readonly struct EventValue
{
    private readonly string? _text;
    private readonly long _integer;
    private readonly double _number;
    private readonly bool _boolean;

    private EventValue(EventValueKind kind, string? text, long integer, double number, bool boolean)
    {
        Kind = kind;
        _text = text;
        _integer = integer;
        _number = number;
        _boolean = boolean;
    }

    /// <summary>Un texto.</summary>
    /// <param name="value">El texto, que no puede ser <see langword="null"/>.</param>
    /// <returns>El valor.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="value"/> es <see langword="null"/>.</exception>
    public static EventValue Of(string value) =>
        new(EventValueKind.Text, value ?? throw new ArgumentNullException(nameof(value)), 0, 0, false);

    /// <summary>Un entero.</summary>
    /// <param name="value">El entero.</param>
    /// <returns>El valor.</returns>
    public static EventValue Of(long value) => new(EventValueKind.Integer, null, value, 0, false);

    /// <summary>Un decimal. Se escribe con el formato de <c>repr</c> de Python (ADR 0023).</summary>
    /// <param name="value">El decimal; también <c>NaN</c> y los infinitos, como en la 0.1.</param>
    /// <returns>El valor.</returns>
    public static EventValue Of(double value) => new(EventValueKind.Number, null, 0, value, false);

    /// <summary>Un booleano.</summary>
    /// <param name="value">El booleano.</param>
    /// <returns>El valor.</returns>
    public static EventValue Of(bool value) => new(EventValueKind.Boolean, null, 0, 0, value);

    internal EventValueKind Kind { get; }

    /// <summary>Escribe el valor en JSON canónico.</summary>
    /// <param name="builder">Donde se escribe.</param>
    /// <exception cref="InvalidOperationException">Si el valor es el valor por defecto; no se llega, porque <see cref="EventLog.Emit"/> lo rechaza antes.</exception>
    internal void WriteJson(StringBuilder builder)
    {
        switch (Kind)
        {
            case EventValueKind.Text:
                CanonicalJson.WriteString(builder, _text ?? throw new InvalidOperationException("Texto sin valor."));
                break;
            case EventValueKind.Integer:
                builder.Append(CanonicalJson.FormatInteger(_integer));
                break;
            case EventValueKind.Number:
                builder.Append(CanonicalJson.FormatDouble(_number));
                break;
            case EventValueKind.Boolean:
                builder.Append(_boolean ? "true" : "false");
                break;
            default:
                throw new InvalidOperationException("Un valor por defecto no se puede escribir: Emit lo rechaza antes.");
        }
    }
}
