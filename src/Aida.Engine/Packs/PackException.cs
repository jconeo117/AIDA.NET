using System.Collections.Immutable;
using System.Text;

namespace Aida.Engine.Packs;

/// <summary>Un pack que no se pudo cargar, con todos sus problemas.</summary>
/// <remarks>
/// Lleva la lista completa, no el primer error: diseñar un pack es un ciclo de ajuste y prueba (<c>engine/pack.py:1-14</c>).
/// </remarks>
public sealed class PackException : Exception
{
    internal PackException(ImmutableArray<PackProblem> problems)
        : base(Summarize(problems))
    {
        Problems = problems;
    }

    /// <summary>Los problemas, en el orden en que la 0.1 los informaría.</summary>
    public ImmutableArray<PackProblem> Problems { get; }

    private static string Summarize(ImmutableArray<PackProblem> problems)
    {
        var builder = new StringBuilder("pack inválido:");
        foreach (var problem in problems)
        {
            builder.Append("\n  - ").Append(problem.Path.Length == 0 ? string.Empty : problem.Path + ": ").Append(problem.Message);
        }

        return builder.ToString();
    }
}
