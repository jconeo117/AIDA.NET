using System.Collections.Frozen;
using Aida.Engine.Ontology;

namespace Aida.Engine.Actions;

/// <summary>Las acciones que declara un brazo, cada una con su reversibilidad.</summary>
/// <remarks>
/// Lo declara el pack del brazo (<c>packs/&lt;brazo&gt;/arm.yaml</c>, ADR 0035), con los mismos nombres de reversibilidad
/// que el pack del mundo. La puerta solo ejecuta lo que está aquí: una acción que nadie declaró se rechaza antes de
/// llegar a la herramienta. En la 0.1 era el <c>frozenset</c> de <c>ACTIONS</c> (<c>tool.py:30-43</c>), sin
/// reversibilidad.
/// </remarks>
public sealed class ActionCatalog
{
    private readonly FrozenDictionary<ActionId, Reversibility> _actions;

    private ActionCatalog(FrozenDictionary<ActionId, Reversibility> actions) => _actions = actions;

    /// <summary>Cuántas acciones hay.</summary>
    public int Count => _actions.Count;

    /// <summary>Construye el catálogo a partir de lo que declara el pack.</summary>
    /// <param name="declared">Pares de identificador y nombre de reversibilidad, en el orden del pack.</param>
    /// <param name="source">De dónde salen, para el mensaje de error (la ruta del archivo).</param>
    /// <returns>El catálogo.</returns>
    /// <exception cref="InvalidDataException">Si no hay ninguna acción, si un identificador está vacío o repetido, o si una
    /// reversibilidad no existe. El mensaje empieza por <paramref name="source"/> y dice todos los problemas.</exception>
    public static ActionCatalog Parse(IEnumerable<KeyValuePair<string, string>> declared, string source)
    {
        ArgumentNullException.ThrowIfNull(declared);
        var problems = new List<string>();
        var actions = new Dictionary<ActionId, Reversibility>();
        foreach (var (id, wire) in declared)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                problems.Add("una acción no tiene identificador");
            }
            else if (!OntologyNames.TryParse(wire ?? string.Empty, out Reversibility reversibility))
            {
                problems.Add($"la acción «{id}» tiene una reversibilidad desconocida: «{wire}»");
            }
            else if (!actions.TryAdd(new ActionId(id), reversibility))
            {
                problems.Add($"la acción «{id}» está repetida");
            }
        }

        if (actions.Count == 0 && problems.Count == 0)
        {
            problems.Add("no declara ninguna acción");
        }

        if (problems.Count > 0)
        {
            throw new InvalidDataException($"{source}: {string.Join("; ", problems)}");
        }

        return new ActionCatalog(actions.ToFrozenDictionary());
    }

    /// <summary>La reversibilidad de una acción, si está declarada.</summary>
    /// <param name="action">La acción.</param>
    /// <param name="reversibility">Su reversibilidad.</param>
    /// <returns><see langword="true"/> si está declarada.</returns>
    public bool TryGet(ActionId action, out Reversibility reversibility) => _actions.TryGetValue(action, out reversibility);
}
