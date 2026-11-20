using System.Globalization;
using Aida.Engine.Text;
using Xunit;

namespace Aida.Engine.Tests.Text;

public sealed class TextFoldTests
{
    [Fact]
    public void Plegar_quita_tildes_y_pasa_a_minusculas()
    {
        Assert.Equal("gomez", TextFold.Fold("Gómez"));
    }

    [Fact]
    public void Plegar_convierte_enie_en_n_y_acentos_en_nandu()
    {
        Assert.Equal("nandu", TextFold.Fold("ÑANDÚ"));
    }

    [Fact]
    public void Plegar_mantiene_eszett_y_digitos()
    {
        Assert.Equal("straße 42", TextFold.Fold("Straße 42"));
    }

    [Fact]
    public void Plegar_es_independiente_de_la_cultura_del_hilo()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            // En tr-TR, ToLower convierte 'I' en 'ı' (dotless i).
            // ToLowerInvariant convierte 'I' en 'i'.
            Assert.Equal("i", TextFold.Fold("I"));
            Assert.Equal("gomez", TextFold.Fold("GÓMEZ"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Plegar_con_null_lanza_excepcion()
    {
        Assert.Throws<ArgumentNullException>(() => TextFold.Fold(null!));
    }

    [Fact]
    public void Plegar_cadena_vacia_devuelve_vacio()
    {
        Assert.Equal(string.Empty, TextFold.Fold(string.Empty));
    }
}
