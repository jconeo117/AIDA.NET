namespace Aida.Engine.Text;

/// <summary>Una palabra de un texto y dónde está en el original.</summary>
/// <param name="Token">La palabra, en minúsculas invariantes.</param>
/// <param name="Start">El índice (UTF-16) de su primer carácter en el texto original.</param>
/// <param name="End">El índice siguiente al último.</param>
/// <remarks>Es la tupla de <c>token_spans</c> de la 0.1 (<c>textmatch.py:21-31</c>). Existe para quien necesita el tramo
/// tal como se escribió (el nombre de una persona nueva), y no solo la bolsa de palabras. Los índices son de UTF-16 y
/// no de puntos de código: coinciden con la 0.1 en todo texto sin caracteres fuera del plano básico.</remarks>
public readonly record struct TokenSpan(string Token, int Start, int End);
