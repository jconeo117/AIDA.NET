using System.Text.RegularExpressions;
using Aida.Guards;
using Xunit;

namespace Aida.ArchitectureTests;

public sealed class LanguageLiteralRuleTests
{
    internal static readonly Regex LanguageLiteral = new(
        @"\""[a-zA-ZáéíóúüñÁÉÍÓÚÜÑ]+\""",
        RegexOptions.CultureInvariant);

    private static readonly SourceTree _sourceTree = new(RepositoryRoot.Find(AppContext.BaseDirectory));

    [Fact]
    public void En_interpretacion_no_hay_literales_de_idioma()
    {
        var violations = new List<string>();
        foreach (var file in _sourceTree.ListFiles("src/Aida.Cognition/Interpretation", [".cs"]))
        {
            var lines = _sourceTree.ReadAllText(file).ReplaceLineEndings("\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("///", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (LanguageLiteral.IsMatch(lines[i]))
                {
                    violations.Add($"{file}:{i + 1}: contiene un literal de idioma en el código: «{lines[i].Trim()}»");
                }
            }
        }

        Assert.True(violations.Count == 0, "\n" + string.Join("\n", violations));
    }

    [Theory]
    [InlineData("\"el\"")]
    [InlineData("\"martes\"")]
    [InlineData("\"tarde\"")]
    public void El_patron_detecta_palabras_de_idioma(string literal) =>
        Assert.Matches(LanguageLiteral, literal);

    [Theory]
    [InlineData("\"HH:mm\"")]
    [InlineData("\":\"")]
    [InlineData("\"/\"")]
    [InlineData("\"-\"")]
    [InlineData("\"\"")]
    public void El_patron_no_confunde_simbolos_ni_formatos(string literal) =>
        Assert.DoesNotMatch(LanguageLiteral, literal);
}
