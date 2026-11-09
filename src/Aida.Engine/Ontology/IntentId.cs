namespace Aida.Engine.Ontology;

/// <summary>Identificador de una intención del catálogo cerrado de lo que alguien puede pedir.</summary>
/// <param name="Value">El identificador tal como lo declara el pack.</param>
/// <remarks>
/// Se escribe como <see cref="AgentId"/>. En la 0.1 era un <c>NewType</c> (<c>engine/ontology.py:35</c>).
/// </remarks>
public readonly record struct IntentId(string Value)
{
    /// <summary>El identificador.</summary>
    /// <exception cref="ArgumentNullException">Si se construye con <see langword="null"/>.</exception>
    public string Value { get; } = Value ?? throw new ArgumentNullException(nameof(Value));
}
