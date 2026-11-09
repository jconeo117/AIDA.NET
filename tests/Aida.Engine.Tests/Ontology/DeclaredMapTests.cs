using Aida.Engine.Ontology;

namespace Aida.Engine.Tests.Ontology;

public sealed class DeclaredMapTests
{
    [Fact]
    public void DeclaredMap_recorre_en_orden_de_declaracion()
    {
        var entries = new[]
        {
            KeyValuePair.Create("z", 10),
            KeyValuePair.Create("a", 20),
            KeyValuePair.Create("m", 30),
        };

        var map = new DeclaredMap<string, int>(entries);

        Assert.Equal(3, map.Count);
        Assert.Equal(new[] { "z", "a", "m" }, map.Keys);
        Assert.Equal(new[] { 10, 20, 30 }, map.Values);
        Assert.Equal(new[] { "z", "a", "m" }, map.Select(pair => pair.Key));
        Assert.Equal(new[] { 10, 20, 30 }, map.Select(pair => pair.Value));
        Assert.Equal(20, map["a"]);
        Assert.True(map.ContainsKey("m"));
        Assert.False(map.ContainsKey("no_existe"));
    }
}
