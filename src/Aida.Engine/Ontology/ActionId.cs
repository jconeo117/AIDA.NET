namespace Aida.Engine.Ontology;

/// <summary>Identificador de una acción del catálogo del pack.</summary>
/// <param name="Value">El identificador tal como lo declara el pack.</param>
/// <remarks>
/// Es un tipo propio y no un <see cref="string"/> para que pasar el identificador de un agente donde va una
/// acción sea un error de compilación; en la 0.1 era un <c>NewType</c> (<c>engine/ontology.py:32</c>).
/// Un evento puede no llevar acción; si la lleva, <see cref="Events.EventLog.Emit"/> rechaza el valor por defecto,
/// que es el único que deja <see cref="Value"/> a <see langword="null"/>.
/// </remarks>
public readonly record struct ActionId(string Value)
{
    /// <summary>El identificador.</summary>
    /// <exception cref="ArgumentNullException">Si se construye con <see langword="null"/>.</exception>
    public string Value { get; } = Value ?? throw new ArgumentNullException(nameof(Value));
}
