using Aida.Engine.Actions;
using Aida.Engine.Ontology;
using Xunit;

namespace Aida.Engine.Tests.Actions;

public sealed class ActionCatalogTests
{
    [Fact]
    public void Lee_pares_validos()
    {
        var declared = new[]
        {
            KeyValuePair.Create("agendar", "reversible"),
            KeyValuePair.Create("cancelar", "reversible_with_cost"),
            KeyValuePair.Create("purgar", "irreversible"),
        };

        var catalog = ActionCatalog.Parse(declared, "arm.yaml");
        Assert.Equal(3, catalog.Count);
        Assert.True(catalog.TryGet(new ActionId("agendar"), out var r1) && r1 == Reversibility.Reversible);
        Assert.True(catalog.TryGet(new ActionId("cancelar"), out var r2) && r2 == Reversibility.ReversibleWithCost);
        Assert.True(catalog.TryGet(new ActionId("purgar"), out var r3) && r3 == Reversibility.Irreversible);
        Assert.False(catalog.TryGet(new ActionId("otra"), out _));
    }

    [Fact]
    public void Los_catalogos_que_no_valen()
    {
        const string source = "packs/agenda/arm.yaml";

        // 1. Vacío
        var exEmpty = Assert.Throws<InvalidDataException>(() =>
            ActionCatalog.Parse(Array.Empty<KeyValuePair<string, string>>(), source));
        Assert.StartsWith(source, exEmpty.Message);
        Assert.Contains("no declara ninguna acción", exEmpty.Message);

        // 2. Múltiples problemas a la vez: id en blanco, repetido, reversibilidad desconocida
        var declaredWithErrors = new[]
        {
            KeyValuePair.Create("", "reversible"),
            KeyValuePair.Create("accion_1", "reversible"),
            KeyValuePair.Create("accion_1", "reversible"),
            KeyValuePair.Create("accion_2", "desconocida"),
        };

        var exMultiple = Assert.Throws<InvalidDataException>(() =>
            ActionCatalog.Parse(declaredWithErrors, source));
        Assert.StartsWith(source, exMultiple.Message);
        Assert.Contains("una acción no tiene identificador", exMultiple.Message);
        Assert.Contains("la acción «accion_1» está repetida", exMultiple.Message);
        Assert.Contains("la acción «accion_2» tiene una reversibilidad desconocida: «desconocida»", exMultiple.Message);
    }
}
