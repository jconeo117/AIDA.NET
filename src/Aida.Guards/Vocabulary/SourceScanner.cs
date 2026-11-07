using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aida.Guards.Vocabulary;

/// <summary>Extrae de un archivo C# los textos que miran A2 y la regla de concatenaciones.</summary>
/// <remarks>
/// Con Roslyn (ADR 0019), y no con expresiones regulares, para distinguir identificador, cadena y
/// comentario, que tienen alcances distintos. Porta <c>_scannable</c> y
/// <c>_detect_concatenated_literal_evasions</c> de Python (<c>test_agnosticism.py:177-270</c>).
/// </remarks>
public static class SourceScanner
{
    /// <summary>Escanea un archivo.</summary>
    /// <param name="sourceText">El código.</param>
    /// <returns>Los textos por posición en el archivo y, a igual posición, por el orden de <see cref="ScannedKind"/>.</returns>
    /// <remarks>
    /// Se analiza con <c>LanguageVersion.Latest</c> y <c>DocumentationMode.Parse</c>, solo sintaxis. Los
    /// identificadores y literales se toman de los tokens <b>sin</b> descender a la trivia, para que un
    /// <c>cref</c> cuente como comentario. Los comentarios son la trivia <c>SingleLineComment</c>,
    /// <c>MultiLineComment</c>, <c>SingleLineDocumentationComment</c> y <c>MultiLineDocumentationComment</c>,
    /// con su texto completo. Una <c>Concatenation</c> sale de una suma cuyos dos lados son literales de
    /// cadena o sumas de ellos (una sola entrada, la exterior), o de una llamada a <c>string.Concat</c> o
    /// <c>string.Join</c> (también con <c>String.</c> y <c>System.String.</c>) cuyos argumentos son todos
    /// literales o, en <c>Join</c>, un separador literal y un único array o colección de literales. Los
    /// literales sueltos salen además como <c>StringLiteral</c>.
    /// </remarks>
    public static IReadOnlyList<ScannedText> Scan(string sourceText)
    {
        var tree = CSharpSyntaxTree.ParseText(
            sourceText,
            new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Parse));
        var root = tree.GetCompilationUnitRoot();
        var found = new List<(int Position, ScannedText Text)>();
        CollectTokens(tree, root, found);
        CollectConcatenations(tree, root, found);
        return found
            .OrderBy(entry => entry.Position)
            .ThenBy(entry => entry.Text.Kind)
            .Select(entry => entry.Text)
            .ToList();
    }

    private static void CollectTokens(SyntaxTree tree, CompilationUnitSyntax root, List<(int Position, ScannedText Text)> found)
    {
        foreach (var token in root.DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.IdentifierToken))
            {
                Add(found, tree, token.SpanStart, ScannedKind.Identifier, token.ValueText);
            }
            else if (IsStringToken(token))
            {
                Add(found, tree, token.SpanStart, ScannedKind.StringLiteral, token.ValueText);
            }

            foreach (var trivia in token.LeadingTrivia.Concat(token.TrailingTrivia))
            {
                if (IsComment(trivia))
                {
                    Add(found, tree, trivia.SpanStart, ScannedKind.Comment, trivia.ToFullString());
                }
            }
        }
    }

    private static void CollectConcatenations(SyntaxTree tree, CompilationUnitSyntax root, List<(int Position, ScannedText Text)> found)
    {
        foreach (var node in root.DescendantNodes())
        {
            if (node is BinaryExpressionSyntax binary && IsOutermostConcatenation(binary))
            {
                Add(found, tree, binary.SpanStart, ScannedKind.Concatenation, SumValue(binary));
            }
            else if (node is InvocationExpressionSyntax invocation && TryJoinCall(invocation, out var joined))
            {
                Add(found, tree, invocation.SpanStart, ScannedKind.Concatenation, joined);
            }
        }
    }

    private static void Add(List<(int Position, ScannedText Text)> found, SyntaxTree tree, int position, ScannedKind kind, string text) =>
        found.Add((position, new ScannedText(tree.GetText().Lines.GetLinePosition(position).Line + 1, kind, text)));

    private static bool IsStringToken(SyntaxToken token) =>
        token.Kind() is SyntaxKind.StringLiteralToken
            or SyntaxKind.Utf8StringLiteralToken
            or SyntaxKind.SingleLineRawStringLiteralToken
            or SyntaxKind.MultiLineRawStringLiteralToken
            or SyntaxKind.Utf8SingleLineRawStringLiteralToken
            or SyntaxKind.Utf8MultiLineRawStringLiteralToken
            or SyntaxKind.InterpolatedStringTextToken;

    private static bool IsComment(SyntaxTrivia trivia) =>
        trivia.Kind() is SyntaxKind.SingleLineCommentTrivia
            or SyntaxKind.MultiLineCommentTrivia
            or SyntaxKind.SingleLineDocumentationCommentTrivia
            or SyntaxKind.MultiLineDocumentationCommentTrivia;

    private static bool IsOutermostConcatenation(BinaryExpressionSyntax binary) =>
        IsLiteralSum(binary) && !(binary.Parent is BinaryExpressionSyntax parent && IsLiteralSum(parent));

    private static bool IsLiteralSum(ExpressionSyntax expression) =>
        IsStringLiteral(expression)
        || (expression is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.AddExpression)
            && IsLiteralSum(binary.Left)
            && IsLiteralSum(binary.Right));

    private static bool IsStringLiteral(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal
        && literal.Kind() is SyntaxKind.StringLiteralExpression or SyntaxKind.Utf8StringLiteralExpression;

    private static string SumValue(ExpressionSyntax expression) =>
        expression is BinaryExpressionSyntax binary
            ? SumValue(binary.Left) + SumValue(binary.Right)
            : ((LiteralExpressionSyntax)expression).Token.ValueText;

    private static bool TryJoinCall(InvocationExpressionSyntax invocation, out string joined)
    {
        joined = string.Empty;
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || !IsStringType(member.Expression))
        {
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments.Select(argument => argument.Expression).ToList();
        switch (member.Name.Identifier.ValueText)
        {
            case "Concat" when arguments.All(IsStringLiteral):
                joined = string.Concat(arguments.Select(LiteralValue));
                return true;
            case "Join" when arguments.Count >= 2 && IsStringLiteral(arguments[0]):
                var items = JoinItems(arguments.Skip(1).ToList());
                if (items is null)
                {
                    return false;
                }

                joined = string.Join(LiteralValue(arguments[0]), items);
                return true;
            default:
                return false;
        }
    }

    // Join acepta los valores sueltos o un único array o colección de literales.
    private static IReadOnlyList<string>? JoinItems(IReadOnlyList<ExpressionSyntax> rest)
    {
        if (rest.Count == 1 && ElementsOf(rest[0]) is { } elements)
        {
            return elements;
        }

        return rest.All(IsStringLiteral) ? rest.Select(LiteralValue).ToList() : null;
    }

    private static IReadOnlyList<string>? ElementsOf(ExpressionSyntax expression)
    {
        IEnumerable<ExpressionSyntax>? items = expression switch
        {
            CollectionExpressionSyntax collection when collection.Elements.All(element => element is ExpressionElementSyntax) =>
                collection.Elements.Cast<ExpressionElementSyntax>().Select(element => element.Expression),
            ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer.Expressions,
            ArrayCreationExpressionSyntax array when array.Initializer is not null => array.Initializer.Expressions,
            _ => null,
        };
        if (items is null)
        {
            return null;
        }

        var values = items.ToList();
        return values.All(IsStringLiteral) ? values.Select(LiteralValue).ToList() : null;
    }

    private static bool IsStringType(ExpressionSyntax expression) =>
        expression.ToString() is "string" or "String" or "System.String";

    private static string LiteralValue(ExpressionSyntax expression) =>
        ((LiteralExpressionSyntax)expression).Token.ValueText;
}
