using System.Collections.Frozen;

namespace Aida.Guards.Dependencies;

/// <summary>Qué proyecto de <c>src/</c> puede referenciar a cuál (regla C3).</summary>
/// <remarks>
/// Es el diagrama de <c>docs/architecture/README.md</c> escrito como datos. Un proyecto que no está aquí es
/// un incumplimiento: un proyecto nuevo obliga a decidir sus dependencias (ADR 0011, ADR 0015).
/// </remarks>
public static class DependencyPolicy
{
    /// <summary>Para cada proyecto, los proyectos que puede referenciar directamente.</summary>
    /// <remarks>En colecciones congeladas: es estado estático, y C4 solo admite el que no cambia.</remarks>
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> Allowed { get; } =
        new[]
        {
            Entry("Aida.Engine"),
            Entry("Aida.Guards"),
            Entry("Aida.Platform", "Aida.Engine"),
            Entry("Aida.Capture", "Aida.Engine", "Aida.Platform"),
            Entry("Aida.Cognition", "Aida.Engine", "Aida.Platform"),
            Entry("Aida.Channel", "Aida.Engine", "Aida.Platform"),
            Entry("Aida.Arms.Health", "Aida.Engine", "Aida.Platform"),
            Entry("Aida.Arms.Agenda", "Aida.Engine", "Aida.Platform"),
            Entry(
                "Aida.Web",
                "Aida.Engine", "Aida.Platform", "Aida.Capture", "Aida.Cognition",
                "Aida.Channel", "Aida.Arms.Health", "Aida.Arms.Agenda"),
            Entry(
                "Aida.Cli",
                "Aida.Engine", "Aida.Platform", "Aida.Capture", "Aida.Cognition",
                "Aida.Channel", "Aida.Arms.Health", "Aida.Arms.Agenda"),
        }.ToFrozenDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

    private static KeyValuePair<string, IReadOnlySet<string>> Entry(string project, params string[] allowed) =>
        new(project, allowed.ToFrozenSet(StringComparer.Ordinal));
}
