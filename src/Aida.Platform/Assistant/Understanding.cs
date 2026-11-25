namespace Aida.Platform.Assistant;

/// <summary>Lo que se entendió de una frase del operador. No decide nada: lo lee el brazo para proponer.</summary>
/// <param name="Intent">El id de la intención del catálogo, o <see langword="null"/> si nada alcanzó el umbral.</param>
/// <param name="Phone">Los dígitos del primer teléfono de la frase, o <see langword="null"/>.</param>
/// <param name="Words">Todas las palabras, en orden.</param>
/// <param name="When">Los momentos, en el orden de la frase.</param>
/// <remarks>Es lo que un modelo de lenguaje producirá más adelante en lugar del clasificador. Lo que propone el brazo
/// no depende de quién lo entendió (ADR 0037).</remarks>
public sealed record Understanding(string? Intent, string? Phone, IReadOnlyList<Word> Words, IReadOnlyList<WhenMention> When);
