using System.Text.Json;

namespace Aida.Guards.Dependencies;

/// <summary>Lee los paquetes de un <c>packages.lock.json</c>.</summary>
/// <remarks>
/// Se lee el lock y no el csproj porque el lock trae también los transitivos: un paquete de modelo que
/// entra por otro paquete también cuenta.
/// </remarks>
public static class LockFileReader
{
    /// <summary>Devuelve los ids de paquete del lock.</summary>
    /// <param name="lockJson">El contenido del archivo.</param>
    /// <returns>Los ids bajo <c>dependencies.&lt;marco&gt;</c>, de todos los marcos, cuyo <c>type</c> no es <c>Project</c>, sin repetidos y ordenados por ordinal.</returns>
    /// <exception cref="System.Text.Json.JsonException">Si el JSON no es válido o no tiene <c>dependencies</c>.</exception>
    /// <remarks>Con <c>System.Text.Json</c>, de la base: no hace falta otra dependencia (ADR 0017).</remarks>
    public static IReadOnlyList<string> ReadPackages(string lockJson)
    {
        using var document = JsonDocument.Parse(lockJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("dependencies", out var dependencies)
            || dependencies.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("El packages.lock.json no tiene la sección «dependencies».");
        }

        var ids = new List<string>();
        foreach (var framework in dependencies.EnumerateObject())
        {
            if (framework.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var package in framework.Value.EnumerateObject())
            {
                if (!IsProject(package.Value))
                {
                    ids.Add(package.Name);
                }
            }
        }

        return ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    private static bool IsProject(JsonElement package) =>
        package.ValueKind == JsonValueKind.Object
        && package.TryGetProperty("type", out var type)
        && type.ValueKind == JsonValueKind.String
        && type.GetString() == "Project";
}
