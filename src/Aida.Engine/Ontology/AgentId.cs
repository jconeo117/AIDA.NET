namespace Aida.Engine.Ontology;

/// <summary>Identificador de un agente: el dueño de cada hecho que registra el motor.</summary>
/// <param name="Value">El identificador tal como lo declara el pack.</param>
/// <remarks>
/// Es un tipo propio y no un <see cref="string"/> para que pasar el identificador de una acción donde va
/// un dueño sea un error de compilación; en la 0.1 era un <c>NewType</c> (<c>engine/ontology.py:33</c>).
/// Todo evento tiene dueño (ADR 0006): <see cref="Events.EventLog.Emit"/> rechaza el valor por defecto,
/// que es el único que deja <see cref="Value"/> a <see langword="null"/>.
/// </remarks>
public readonly record struct AgentId(string Value)
{
    /// <summary>El identificador.</summary>
    /// <exception cref="ArgumentNullException">Si se construye con <see langword="null"/>.</exception>
    public string Value { get; } = Value ?? throw new ArgumentNullException(nameof(Value));
}
