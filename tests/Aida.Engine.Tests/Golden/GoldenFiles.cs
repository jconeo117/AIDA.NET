using System.Text;
using Aida.Engine.Events;

namespace Aida.Engine.Tests.Golden;

internal static class GoldenFiles
{
    private static readonly Lazy<string> _repoRoot = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aida.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio que contiene Aida.slnx.");
    });

    internal static string RepoRoot => _repoRoot.Value;

    internal static string GoldenDir => Path.Combine(RepoRoot, "tests", "golden");

    internal static string[] ReadLines(string relativePath) =>
        File.ReadAllLines(Path.Combine(GoldenDir, relativePath));

    internal static byte[] ReadBytes(string relativePath) =>
        File.ReadAllBytes(Path.Combine(GoldenDir, relativePath));

    internal static EventKind ParseWireKind(string wire) => wire switch
    {
        "action_started" => EventKind.ActionStarted,
        "action_completed" => EventKind.ActionCompleted,
        "action_requested" => EventKind.ActionRequested,
        "action_rejected" => EventKind.ActionRejected,
        "action_failed" => EventKind.ActionFailed,
        "action_aborted" => EventKind.ActionAborted,
        "action_proposed" => EventKind.ActionProposed,
        "action_authorized" => EventKind.ActionAuthorized,
        "action_denied" => EventKind.ActionDenied,
        "action_expired" => EventKind.ActionExpired,
        "need_escalated" => EventKind.NeedEscalated,
        "input_observed" => EventKind.InputObserved,
        "intent_classified" => EventKind.IntentClassified,
        _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, "Tipo de evento desconocido en el cable."),
    };

    internal static string FormatDifferences(IReadOnlyList<(int Line, string Expected, string Actual)> diffs, int max = 5)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Se encontraron {diffs.Count} diferencias. Mostrando las primeras {Math.Min(diffs.Count, max)}:");
        for (var i = 0; i < Math.Min(diffs.Count, max); i++)
        {
            var (line, expected, actual) = diffs[i];
            sb.AppendLine($"[Línea {line}]");
            sb.AppendLine($"  Esperado: {expected}");
            sb.AppendLine($"  Obtenido: {actual}");
        }

        return sb.ToString();
    }
}
