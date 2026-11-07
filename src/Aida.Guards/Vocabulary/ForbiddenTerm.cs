namespace Aida.Guards.Vocabulary;

/// <summary>Un término prohibido: una o varias palabras seguidas.</summary>
/// <param name="Words">Las palabras, ya plegadas con <see cref="TextFold.Fold"/>.</param>
/// <param name="Scope">Dónde se busca.</param>
/// <remarks>Varias palabras para los ids compuestos del pack de plantilla (<c>espacio_libre</c>).</remarks>
public sealed record ForbiddenTerm(IReadOnlyList<string> Words, TermScope Scope);
