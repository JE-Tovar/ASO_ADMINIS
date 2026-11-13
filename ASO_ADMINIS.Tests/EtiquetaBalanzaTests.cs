using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Tests;

public class EtiquetaBalanzaTests
{
    private static string Etiqueta(string doce) => doce + EtiquetaBalanza.CalcularDigito(doce);

    [Fact]
    public void Lee_producto_y_peso_en_gramos()
    {
        Assert.True(EtiquetaBalanza.Leer(Etiqueta("20" + "00123" + "01250"), new FormatoEtiqueta(), out var plu, out var peso));
        Assert.Equal("00123", plu);
        Assert.Equal(1.250m, peso);
    }

    [Fact]
    public void Calcula_el_digito_verificador_EAN13()
    {
        // 4006381333931 es un EAN-13 real de ejemplo.
        Assert.Equal(1, EtiquetaBalanza.CalcularDigito("400638133393"));
        Assert.True(EtiquetaBalanza.DigitoVerificadorValido("4006381333931"));
    }

    [Fact]
    public void Rechaza_digito_verificador_malo()
    {
        var buena = Etiqueta("20" + "00123" + "01250");
        var mala = buena[..12] + ((buena[12] - '0' + 1) % 10);
        Assert.False(EtiquetaBalanza.Leer(mala, new FormatoEtiqueta(), out _, out _));
    }

    [Theory]
    [InlineData("20012301250")]        // corta
    [InlineData("ACE-15W40")]         // un código de artículo cualquiera
    public void Lo_que_no_son_13_digitos_no_es_etiqueta(string texto) =>
        Assert.False(EtiquetaBalanza.Leer(texto, new FormatoEtiqueta(), out _, out _));

    [Fact]
    public void Un_EAN13_de_otro_prefijo_no_es_etiqueta() =>
        Assert.False(EtiquetaBalanza.Leer("4006381333931", new FormatoEtiqueta(), out _, out _));

    [Fact]
    public void Respeta_el_formato_configurado()
    {
        // Prefijo 21, 4 dígitos de producto, 6 de peso con 3 decimales.
        var formato = new FormatoEtiqueta("21", 4, 3);
        Assert.True(EtiquetaBalanza.Leer(Etiqueta("21" + "0042" + "002500"), formato, out var plu, out var peso));
        Assert.Equal("0042", plu);
        Assert.Equal(2.5m, peso);
    }

    [Fact]
    public void El_PLU_se_compara_sin_ceros_a_la_izquierda() =>
        Assert.Equal(EtiquetaBalanza.NormalizarPlu("00123"), EtiquetaBalanza.NormalizarPlu("123"));
}
