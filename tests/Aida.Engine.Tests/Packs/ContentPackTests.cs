using Aida.Engine.Packs;
using Aida.Engine.Tests.Golden;

namespace Aida.Engine.Tests.Packs;

public sealed class ContentPackTests
{
    [Fact]
    public void ActionById_indexa_todas_las_acciones()
    {
        var packPath = Path.Combine(GoldenFiles.RepoRoot, "packs", "_plantilla", "pack.yaml");
        var pack = PackLoader.Load(packPath);

        Assert.Equal(pack.Actions.Length, pack.ActionById.Count);
        foreach (var action in pack.Actions)
        {
            Assert.True(pack.ActionById.ContainsKey(action.Id));
            Assert.Same(action, pack.ActionById[action.Id]);
        }
    }

    [Fact]
    public void IntentById_indexa_todas_las_intenciones()
    {
        var packPath = Path.Combine(GoldenFiles.RepoRoot, "packs", "_plantilla", "pack.yaml");
        var pack = PackLoader.Load(packPath);

        Assert.Equal(pack.Intents.Length, pack.IntentById.Count);
        foreach (var intent in pack.Intents)
        {
            Assert.True(pack.IntentById.ContainsKey(intent.Id));
            Assert.Same(intent, pack.IntentById[intent.Id]);
        }
    }
}
