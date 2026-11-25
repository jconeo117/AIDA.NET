namespace Aida.Platform.Assistant;

/// <summary>Una palabra de la frase del operador.</summary>
/// <param name="Text">Tal como se escribió: el tramo del original.</param>
/// <param name="Folded">Plegada con <c>TextFold.Fold</c>.</param>
/// <param name="Role">Su papel.</param>
public sealed record Word(string Text, string Folded, WordRole Role);
