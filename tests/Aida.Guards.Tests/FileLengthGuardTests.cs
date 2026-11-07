using Aida.Guards.Shape;

namespace Aida.Guards.Tests;

public sealed class FileLengthGuardTests
{
    [Fact]
    public void Limite_menor_que_uno_lanza_excepcion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileLengthGuard(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileLengthGuard(-1));
    }

    [Fact]
    public void Archivo_vacio_tiene_cero_lineas()
    {
        var guard = new FileLengthGuard(300);
        Assert.Equal(0, guard.CountLines(string.Empty));
    }

    [Fact]
    public void Archivo_de_300_lineas_pasa()
    {
        var guard = new FileLengthGuard(300);
        var lines = string.Join("\n", Enumerable.Range(1, 300).Select(i => $"int x{i} = {i};"));

        Assert.Equal(300, guard.CountLines(lines));
        var violations = guard.Check("src/File.cs", lines);
        Assert.Empty(violations);
    }

    [Fact]
    public void Archivo_de_301_lineas_falla()
    {
        var guard = new FileLengthGuard(300);
        var lines = string.Join("\n", Enumerable.Range(1, 301).Select(i => $"int x{i} = {i};"));

        Assert.Equal(301, guard.CountLines(lines));
        var violations = guard.Check("src/File.cs", lines);

        var violation = Assert.Single(violations);
        Assert.Equal("C1", violation.Rule);
        Assert.Equal("src/File.cs", violation.Location);
        Assert.Equal(0, violation.Line);
        Assert.Contains("301 líneas y el máximo es 300", violation.Message);
    }

    [Fact]
    public void Comentarios_de_documentacion_xml_con_sangria_no_cuentan()
    {
        var guard = new FileLengthGuard(300);
        var xmlLines = string.Join("\n", Enumerable.Repeat("    /// <summary>Comentario</summary>", 20));
        var codeLines = string.Join("\n", Enumerable.Range(1, 300).Select(i => $"int x{i} = {i};"));
        var content = xmlLines + "\n" + codeLines;

        Assert.Equal(300, guard.CountLines(content));
        var violations = guard.Check("src/Documented.cs", content);
        Assert.Empty(violations);
    }

    [Fact]
    public void Comentarios_normales_de_doble_barra_si_cuentan()
    {
        var guard = new FileLengthGuard(300);
        var comments = string.Join("\n", Enumerable.Repeat("// Comentario normal", 301));

        Assert.Equal(301, guard.CountLines(comments));
        var violations = guard.Check("src/Comments.cs", comments);
        Assert.Single(violations);
    }

    [Fact]
    public void Salto_de_linea_final_no_suma_linea_adicional()
    {
        var guard = new FileLengthGuard(300);
        var withoutTrailingNewline = "linea 1\nlinea 2";
        var withTrailingNewline = "linea 1\nlinea 2\n";

        Assert.Equal(2, guard.CountLines(withoutTrailingNewline));
        Assert.Equal(2, guard.CountLines(withTrailingNewline));
    }

    [Fact]
    public void Saltos_crlf_y_lf_cuentan_exactamente_igual()
    {
        var guard = new FileLengthGuard(300);
        var lf = "linea 1\nlinea 2\nlinea 3";
        var crlf = "linea 1\r\nlinea 2\r\nlinea 3";

        Assert.Equal(3, guard.CountLines(lf));
        Assert.Equal(3, guard.CountLines(crlf));
    }
}
