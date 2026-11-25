namespace Aida.Platform.Assistant;

/// <summary>Una intención de un brazo y sus frases de ejemplo.</summary>
/// <param name="Id">El id, que el brazo declara en código.</param>
/// <param name="Examples">Los ejemplos, en el orden del pack.</param>
public sealed record IntentExamples(string Id, IReadOnlyList<string> Examples);
