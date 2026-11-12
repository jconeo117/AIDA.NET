namespace Aida.Engine.Ontology;

/// <summary>Identificador de un rasgo de personalidad declarado por el pack.</summary>
/// <param name="Value">El identificador tal como lo declara el pack.</param>
/// <remarks>
/// Se escribe como <see cref="AgentId"/>, para que un rasgo no pueda usarse donde va una necesidad. En la 0.1
/// era un <c>NewType</c> (<c>engine/ontology.py:28</c>).
/// </remarks>
public readonly record struct TraitId(string Value)
{
    /// <summary>El identificador.</summary>
    /// <exception cref="ArgumentNullException">Si se construye con <see langword="null"/>.</exception>
    public string Value { get; } = Value ?? throw new ArgumentNullException(nameof(Value));
}
