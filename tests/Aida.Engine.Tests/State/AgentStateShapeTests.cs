using System.Collections;
using System.Reflection;
using Aida.Engine.Packs;
using Aida.Engine.State;

namespace Aida.Engine.Tests.State;

public sealed class AgentStateShapeTests
{
    private static readonly HashSet<string> ExpectedProperties = new(StringComparer.Ordinal)
    {
        "AgentId",
        "Needs",
        "Debt",
        "FloorStreak",
        "Traits",
        "Goals",
        "Saturation",
        "Performing",
        "Strain",
        "NeedWeights",
        "Gains",
        "Costs",
        "Caps",
        "Tastes",
        "Temperament",
    };

    private static readonly HashSet<string> ExpectedMethods = new(StringComparer.Ordinal)
    {
        "CapOf",
        "Urgency",
        "Pursuing",
        "ToString",
        "Equals",
        "GetHashCode",
        "GetType",
    };

    [Fact]
    public void La_API_publica_no_muta()
    {
        var type = typeof(AgentState);

        // Sin campos públicos de instancia
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(fields);

        // Sin eventos públicos de instancia
        var events = type.GetEvents(BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(events);

        // Propiedades públicas de instancia
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var propNames = properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(ExpectedProperties, propNames);

        // Ninguna propiedad pública tiene setter público
        foreach (var prop in properties)
        {
            Assert.True(prop.CanRead, $"La propiedad {prop.Name} debe poder leerse");
            Assert.Null(prop.GetSetMethod());
        }

        // Métodos públicos de instancia (excluyendo los get_ de propiedades)
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(ExpectedMethods, methods);

        // Comprobación de sobrecargas de CapOf: exactamente 2
        var capOfOverloads = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "CapOf")
            .ToList();
        Assert.Equal(2, capOfOverloads.Count);

        // Inmutabilidad de los mapas públicos
        const string Yaml = """
            id: p1
            label: P1
            needs:
              - id: n1
                label: N1
                decay_per_tick: 0.1
            actions:
              - id: a1
                label: A1
                reversibility: reversible
                duration_ticks: 1
                satisfies:
                  n1: 0.5
                effects:
                  n1: 0.5
            agents:
              - id: ag1
                label: Ag1
            """;
        var pack = PackLoader.Parse(Yaml);
        var agent = AgentState.FromSpec(pack, pack.Agents[0]);

        var mapPropNames = new[] { "Needs", "Debt", "FloorStreak", "Traits", "Goals", "Saturation", "NeedWeights", "Gains", "Costs", "Caps", "Tastes" };
        foreach (var name in mapPropNames)
        {
            var prop = type.GetProperty(name)!;
            var val = prop.GetValue(agent)!;

            // Si implementa IDictionary no genérico
            if (val is IDictionary nonGenDict)
            {
                Assert.True(nonGenDict.IsReadOnly, $"El mapa {name} debe ser IsReadOnly en IDictionary");
            }

            // Si implementa ICollection<> genérico (donde vive IsReadOnly de IDictionary<,>)
            foreach (var iface in val.GetType().GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(ICollection<>))
                {
                    var isReadOnlyProp = iface.GetProperty("IsReadOnly")!;
                    var isReadOnly = (bool)isReadOnlyProp.GetValue(val)!;
                    Assert.True(isReadOnly, $"El mapa {name} debe ser IsReadOnly en {iface.Name}");
                }
            }
        }
    }
}
