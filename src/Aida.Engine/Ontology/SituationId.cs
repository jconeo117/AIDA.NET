namespace Aida.Engine.Ontology;

/// <summary>Identificador de una situación pasajera.</summary>
/// <param name="Value">El identificador tal como lo declara el pack.</param>
/// <remarks>
/// Se escribe como <see cref="AgentId"/>. En la 0.1 era un <c>NewType</c> (<c>engine/ontology.py:31</c>).
/// </remarks>
public readonly record struct SituationId(string Value)
{
    /// <summary>El identificador.</summary>
    /// <exception cref="ArgumentNullException">Si se construye con <see langword="null"/>.</exception>
    public string Value { get; } = Value ?? throw new ArgumentNullException(nameof(Value));
}
