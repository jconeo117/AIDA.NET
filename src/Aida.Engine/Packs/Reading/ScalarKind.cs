namespace Aida.Engine.Packs.Reading;

/// <summary>Qué tipo le da PyYAML a un escalar, antes de que pydantic lo convierta al campo.</summary>
/// <remarks>
/// Un escalar entre comillas, literal o plegado es siempre <see cref="Text"/>: solo los planos se resuelven
/// (ADR 0024, §A.7). <see cref="Timestamp"/> no lo acepta ningún campo.
/// </remarks>
internal enum ScalarKind
{
    /// <summary>Un texto.</summary>
    Text,

    /// <summary>El nulo: <c>~</c>, <c>null</c> o vacío.</summary>
    Null,

    /// <summary>Un booleano para PyYAML: <c>yes</c>, <c>true</c>, <c>on</c>…</summary>
    Bool,

    /// <summary>Un entero: decimal, binario, hexadecimal, octal o en base 60.</summary>
    Int,

    /// <summary>Un decimal con punto y exponente con signo, o <c>.inf</c> o <c>.nan</c>.</summary>
    Float,

    /// <summary>Una fecha o una fecha con hora.</summary>
    Timestamp,
}
