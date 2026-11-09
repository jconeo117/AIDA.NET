namespace Aida.Engine.Ontology;

/// <summary>Identificador de una meta.</summary>
/// <param name="Value">El identificador tal como lo declara el pack.</param>
/// <remarks>
/// Se escribe como <see cref="AgentId"/>. Una meta puede referirse a otra (<c>requires_goal</c>), así que el
/// tipo no debe confundirse con el de una franja o una situación. En la 0.1 era un <c>NewType</c>
/// (<c>engine/ontology.py:29</c>).
/// </remarks>
public readonly record struct GoalId(string Value)
{
    /// <summary>El identificador.</summary>
    /// <exception cref="ArgumentNullException">Si se construye con <see langword="null"/>.</exception>
    public string Value { get; } = Value ?? throw new ArgumentNullException(nameof(Value));
}
