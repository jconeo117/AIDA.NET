using Aida.Engine.Actions;
using Xunit;

namespace Aida.Engine.Tests.Actions;

public sealed class ToolResultTests
{
    [Fact]
    public void Copia_los_datos()
    {
        var dict = new Dictionary<string, string> { ["clave"] = "valor" };
        var result = ToolResult.Done(dict);

        dict["clave"] = "otro";
        dict["nueva"] = "extra";

        Assert.Equal(ToolStatus.Done, result.Status);
        Assert.Equal(string.Empty, result.Reason);
        Assert.Single(result.Data);
        Assert.Equal("valor", result.Data["clave"]);
    }

    [Fact]
    public void Un_dato_null_lanza()
    {
        Assert.Throws<ArgumentNullException>(() => new ToolResult(ToolStatus.Done, null!));

        var dict = new Dictionary<string, string?> { ["clave"] = null };
        Assert.Throws<ArgumentNullException>(() => new ToolResult(ToolStatus.Done, string.Empty, dict!));
    }
}
