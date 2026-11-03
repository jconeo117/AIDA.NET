namespace Aida.Guards.Vocabulary;

/// <summary>Dónde se busca un término prohibido.</summary>
/// <remarks>
/// La documentación XML está en español, y palabras como «disco» o «memoria» son de uso corriente. Los
/// términos de dominio inequívocos se buscan también en los comentarios (ADR 0002); los ambiguos, solo en
/// el código, como hacía Python.
/// </remarks>
public enum TermScope
{
    /// <summary>Identificadores, cadenas y concatenaciones.</summary>
    Code,

    /// <summary>Lo de <see cref="Code"/> y además los comentarios, con los de documentación XML.</summary>
    Everywhere,
}
