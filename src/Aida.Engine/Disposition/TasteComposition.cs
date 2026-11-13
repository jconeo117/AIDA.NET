using System.Collections.Immutable;
using Aida.Engine.Ontology;
using Aida.Engine.Serialization;

namespace Aida.Engine.Disposition;

/// <summary>Combina los gustos de un agente en un multiplicador por objetivo (<c>engine/disposition.py:357-373</c>).</summary>
/// <remarks>
/// Los gustos hacia el mismo objetivo se multiplican en el orden declarado. El resultado va ordenado por punto de código
/// para que su recorrido no dependa del proceso. Es la única puerta de gustos del agente: se fija al nacer y no depende
/// de los rasgos.
/// </remarks>
internal static class TasteComposition
{
    /// <summary>Un multiplicador por objetivo, acumulando los gustos repetidos.</summary>
    /// <param name="tastes">Los gustos del agente, en el orden del YAML.</param>
    /// <returns>Un valor por objetivo, ordenado por punto de código.</returns>
    internal static SortedDictionary<string, double> Compose(ImmutableArray<TasteSpec> tastes)
    {
        var result = new SortedDictionary<string, double>(CodePointComparer.Instance);
        foreach (var taste in tastes)
        {
            var previous = result.TryGetValue(taste.Target, out var value) ? value : 1.0;
            result[taste.Target] = previous * taste.Multiplier;
        }

        return result;
    }
}
