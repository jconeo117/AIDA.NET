using Aida.Guards.Vocabulary;

namespace Aida.Guards.Tests;

public sealed class SourceScannerTests
{
    [Fact]
    public void Extrae_identificadores_literales_y_comentarios_con_sus_lineas()
    {
        var code = """"
            // Comentario simple
            /* Comentario
               multilinea */
            /// <summary>Documentacion</summary>
            namespace Aida.Engine;

            public sealed class ScannerDemo
            {
                private const string Normal = "texto normal";
                private const string Verbatim = @"texto verbatim";
                private const string Crudo = """
                    texto crudo
                    """;
                private readonly string _interpolado = $"prefijo {"medio"}";
            }
            """";

        var scanned = SourceScanner.Scan(code);

        Assert.Contains(scanned, s => s.Kind == ScannedKind.Comment && s.Text.Contains("Comentario simple"));
        Assert.Contains(scanned, s => s.Kind == ScannedKind.Comment && s.Text.Contains("multilinea"));
        Assert.Contains(scanned, s => s.Kind == ScannedKind.Comment && s.Text.Contains("Documentacion"));
        Assert.Contains(scanned, s => s.Kind == ScannedKind.Identifier && s.Text == "ScannerDemo");
        Assert.Contains(scanned, s => s.Kind == ScannedKind.StringLiteral && s.Text == "texto normal");
        Assert.Contains(scanned, s => s.Kind == ScannedKind.StringLiteral && s.Text == "texto verbatim");
        Assert.Contains(scanned, s => s.Kind == ScannedKind.StringLiteral && s.Text.Contains("texto crudo"));
        Assert.Contains(scanned, s => s.Kind == ScannedKind.StringLiteral && s.Text == "prefijo ");
    }

    [Fact]
    public void Comentario_xml_con_cref_no_extrae_identificador_interno()
    {
        var code = """
            /// <summary>Mira <see cref="PatientRecord"/> para detalles.</summary>
            public class Demo { }
            """;

        var scanned = SourceScanner.Scan(code);

        Assert.DoesNotContain(scanned, s => s.Kind == ScannedKind.Identifier && s.Text == "PatientRecord");
        Assert.Contains(scanned, s => s.Kind == ScannedKind.Comment && s.Text.Contains("PatientRecord"));
    }

    [Fact]
    public void Suma_de_dos_literales_genera_concatenacion()
    {
        var code = """var x = "c" + "pu";""";
        var scanned = SourceScanner.Scan(code);

        var concat = Assert.Single(scanned, s => s.Kind == ScannedKind.Concatenation);
        Assert.Equal("cpu", concat.Text);
    }

    [Fact]
    public void Suma_encadenada_de_multiples_literales_genera_una_sola_concatenacion_exterior()
    {
        var code = """var x = "a" + "b" + "c";""";
        var scanned = SourceScanner.Scan(code);

        var concats = scanned.Where(s => s.Kind == ScannedKind.Concatenation).ToList();
        var concat = Assert.Single(concats);
        Assert.Equal("abc", concat.Text);
    }

    [Fact]
    public void Invocacion_de_string_Concat_y_string_Join_genera_concatenacion()
    {
        var code = """
            var c1 = string.Concat("c", "pu");
            var c2 = string.Join("", "c", "pu");
            var c3 = string.Join("", ["c", "pu"]);
            var c4 = System.String.Concat("c", "pu");
            var c5 = String.Join("-", "a", "b");
            """;

        var scanned = SourceScanner.Scan(code);
        var concats = scanned.Where(s => s.Kind == ScannedKind.Concatenation).ToList();

        Assert.Equal(5, concats.Count);
        Assert.Equal("cpu", concats[0].Text);
        Assert.Equal("cpu", concats[1].Text);
        Assert.Equal("cpu", concats[2].Text);
        Assert.Equal("cpu", concats[3].Text);
        Assert.Equal("a-b", concats[4].Text);
    }
}
