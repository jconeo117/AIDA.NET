using System.Text.RegularExpressions;

namespace Aida.Platform.Snapshots;

/// <summary>Las únicas entradas que puede llevar una copia.</summary>
/// <remarks>Una lista cerrada, más estricta que <c>_safe</c> de la 0.1 (<c>snapshot.py:93-99</c>): solo se acepta lo que
/// este AIDA escribe. Así ninguna entrada puede salir de la carpeta preparada ni pisar la clave envuelta.</remarks>
internal static partial class SnapshotEntries
{
    internal const string Manifest = "manifest.json";
    internal const string Database = "aida.db";
    internal const string Key = "machine.key";
    internal const string ConfigPrefix = "config/";

    internal static bool IsAllowed(string name) =>
        name is Manifest or Database or Key || ConfigName().IsMatch(name);

    [GeneratedRegex("^config/[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    internal static partial Regex ConfigName();
}
