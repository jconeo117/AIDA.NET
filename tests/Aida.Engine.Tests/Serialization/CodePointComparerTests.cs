using Aida.Engine.Serialization;

namespace Aida.Engine.Tests.Serialization;

public sealed class CodePointComparerTests
{
    [Fact]
    public void CodePointComparer_ordena_como_Python()
    {
        var input = new[] { "a", "B", "\ue000", "😀", "", "\ufffd", "\ud7ff", "ab", "a\0" };
        var expected = new[] { "", "B", "a", "a\0", "ab", "\ud7ff", "\ue000", "\ufffd", "😀" };

        var sorted = input.OrderBy(x => x, CodePointComparer.Instance).ToArray();

        Assert.Equal(expected, sorted);
    }

    [Fact]
    public void CodePointComparer_trata_null_y_la_misma_referencia()
    {
        var comparer = CodePointComparer.Instance;
        var s = "texto";

        Assert.Equal(0, comparer.Compare(null, null));
        Assert.Equal(0, comparer.Compare(s, s));
        Assert.True(comparer.Compare(null, "") < 0);
        Assert.True(comparer.Compare("", null) > 0);
        Assert.True(comparer.Compare("a", "b") < 0);
        Assert.True(comparer.Compare("b", "a") > 0);
        Assert.True(comparer.Compare("a", "aa") < 0);
        Assert.True(comparer.Compare("aa", "a") > 0);
    }
}
