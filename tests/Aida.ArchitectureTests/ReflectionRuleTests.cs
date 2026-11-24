using System.Text.RegularExpressions;
using Aida.Guards;

namespace Aida.ArchitectureTests;

public sealed class ReflectionRuleTests
{
    internal static readonly Regex ReflectiveLookup = new(
        @"\b(GetMethods?|GetRuntimeMethods?|GetMembers?|GetConstructors?|InvokeMember|DynamicInvoke|CreateDelegate)\s*\(|\bActivator\s*\.|\bMethodInfo\b|\bDeclaredMethods\b|\bType\s*\.\s*GetType\s*\(",
        RegexOptions.CultureInvariant);

    private static readonly SourceTree _sourceTree = new(RepositoryRoot.Find(AppContext.BaseDirectory));

    [Fact]
    public void Fuera_de_los_guardianes_no_se_buscan_ni_se_invocan_miembros_por_reflexion()
    {
        var violations = new List<string>();
        foreach (var file in _sourceTree.ListFiles("src", [".cs", ".razor"]))
        {
            if (file.StartsWith("src/Aida.Guards/", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = _sourceTree.ReadAllText(file).ReplaceLineEndings("\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (ReflectiveLookup.IsMatch(lines[i]))
                {
                    violations.Add($"{file}:{i + 1}: busca o invoca un miembro por reflexión: «{lines[i].Trim()}»");
                }
            }
        }

        Assert.True(violations.Count == 0, "\n" + string.Join("\n", violations));
    }

    [Theory]
    [InlineData("typeof(SqliteConnection).GetMethods()")]
    [InlineData("type.GetMethod(\"X\")")]
    [InlineData("Activator.CreateInstance(type)")]
    [InlineData("MethodInfo method = null;")]
    [InlineData("var t = Type.GetType(\"X\");")]
    public void El_patron_detecta_la_reflexion(string line) =>
        Assert.Matches(ReflectiveLookup, line);

    [Theory]
    [InlineData("root.TryGetProperty(key, out var element)")]
    [InlineData("typeof(SnapshotManifest).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()")]
    [InlineData("configure?.Invoke(builder);")]
    [InlineData("command.CommandText = \"VACUUM INTO $path;\";")]
    public void El_patron_no_confunde_lo_permitido(string line) =>
        Assert.DoesNotMatch(ReflectiveLookup, line);
}
