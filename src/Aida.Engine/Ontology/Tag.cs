namespace Aida.Engine.Ontology;

/// <summary>Etiqueta libre de una acción, para agrupar o clasificar.</summary>
/// <param name="Value">La etiqueta tal como la escribe el pack.</param>
/// <remarks>
/// Se escribe como <see cref="AgentId"/>. En la 0.1 era un <c>NewType</c> (<c>engine/ontology.py:34</c>). Un
/// conjunto de etiquetas se ordena por punto de código al volcarlo, para que el orden no dependa del proceso.
/// </remarks>
public readonly record struct Tag(string Value)
{
    /// <summary>La etiqueta.</summary>
    /// <exception cref="ArgumentNullException">Si se construye con <see langword="null"/>.</exception>
    public string Value { get; } = Value ?? throw new ArgumentNullException(nameof(Value));
}
