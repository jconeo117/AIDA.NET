namespace Aida.Guards.Vocabulary;

/// <summary>Un trozo de texto del código fuente.</summary>
/// <param name="Line">Línea de inicio, empezando en 1.</param>
/// <param name="Kind">Qué es.</param>
/// <param name="Text">De un literal, su valor; de un comentario, el comentario entero.</param>
/// <remarks>Separa qué se encontró de qué se hace con ello, para probar el escáner sin las listas.</remarks>
public sealed record ScannedText(int Line, ScannedKind Kind, string Text);
