using Aida.Platform.Ledger;
using Xunit;

namespace Aida.Platform.Tests.Ledger;

public sealed class LedgerJsonTests
{
    [Fact]
    public void Separa_con_coma_y_espacio_y_ordena_las_claves()
    {
        var fields = new Dictionary<string, string>
        {
            ["z"] = "last",
            ["a"] = "first",
            ["m"] = "middle",
        };

        var json = LedgerJson.Write(fields);

        Assert.Equal("{\"a\": \"first\", \"m\": \"middle\", \"z\": \"last\"}", json);
    }

    [Fact]
    public void Escapa_como_Python_y_deja_lo_que_no_es_ASCII()
    {
        var fields = new Dictionary<string, string>
        {
            ["escapes"] = "comillas \" y \\ barra\ntab\tret\rbs\bff\fctrl\x01\x1f",
            ["unicode"] = "ñ€😀\u2028\u2029",
        };

        var json = LedgerJson.Write(fields);

        // Debería tener las comillas y barra escapadas, los controles escapados y el unicode sin escapar
        Assert.Equal(
            "{\"escapes\": \"comillas \\\" y \\\\ barra\\ntab\\tret\\rbs\\bff\\fctrl\\u0001\\u001f\", \"unicode\": \"ñ€😀\u2028\u2029\"}",
            json);
    }

    [Fact]
    public void Una_clave_no_ASCII_revienta()
    {
        var fields = new Dictionary<string, string>
        {
            ["clave_con_ñ"] = "valor",
        };

        var ex = Assert.Throws<ArgumentException>(() => LedgerJson.Write(fields));
        Assert.Contains("no es ASCII", ex.Message);
    }
}
