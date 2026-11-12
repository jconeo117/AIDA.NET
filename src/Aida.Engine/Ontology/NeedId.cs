namespace Aida.Engine.Ontology;

/// <summary>Identificador de una necesidad: un eje de presión interna del pack.</summary>
/// <param name="Value">El identificador tal como lo declara el pack.</param>
/// <remarks>
/// Se escribe como <see cref="AgentId"/>: un tipo propio para que pasar una necesidad donde va una meta o un
/// rasgo sea un error de compilación. En la 0.1 era un <c>NewType</c> (<c>engine/ontology.py:27</c>).
/// </remarks>
public readonly record struct NeedId(string Value)
{
    /// <summary>El identificador.</summary>
    /// <exception cref="ArgumentNullException">Si se construye con <see langword="null"/>.</exception>
    public string Value { get; } = Value ?? throw new ArgumentNullException(nameof(Value));
}
