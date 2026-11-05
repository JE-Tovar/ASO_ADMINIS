using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Tests;

public class RifTests
{
    [Theory]
    [InlineData("G-20000303-0")] // RIF del SENIAT
    [InlineData("J-00002961-0")]
    public void Acepta_rif_con_digito_verificador_correcto(string rif) =>
        Assert.True(Rif.EsRifValido(rif));

    [Theory]
    [InlineData("G-20000303-1")]
    [InlineData("J-00002961-5")]
    public void Rechaza_rif_con_digito_verificador_errado(string rif) =>
        Assert.False(Rif.EsRifValido(rif));

    /// <summary>
    /// Un caso por letra sobre los mismos 8 dígitos, con el DV calculado a mano:
    /// 1·3+2·2+3·7+4·6+5·5+6·4+7·3+8·2 = 138, más el valor de la letra × 4.
    /// </summary>
    [Theory]
    [InlineData('V', 1)] // 142 mod 11 = 10 → 11 − 10 = 1
    [InlineData('E', 8)] // 146 mod 11 = 3  → 8
    [InlineData('J', 4)] // 150 mod 11 = 7  → 4
    [InlineData('P', 0)] // 154 mod 11 = 0  → 11 → 0
    [InlineData('G', 7)] // 158 mod 11 = 4  → 7
    public void Digito_verificador_por_letra(char letra, int esperado)
    {
        Assert.Equal(esperado, Rif.DigitoVerificador(letra, "12345678"));
        Assert.True(Rif.EsRifValido($"{letra}-12345678-{esperado}"));
        Assert.False(Rif.EsRifValido($"{letra}-12345678-{(esperado + 1) % 10}"));
    }

    [Fact]
    public void Letra_no_reconocida_no_es_rif() =>
        Assert.Null(Rif.Normalizar("X-12345678-9"));

    [Theory]
    [InlineData("j000029610", "J-00002961-0")]
    [InlineData("j-00002961-0", "J-00002961-0")]
    [InlineData("V 12.345.678", "V-12345678")]
    public void Normaliza_al_formato_con_guiones(string tecleado, string esperado) =>
        Assert.Equal(esperado, Rif.Normalizar(tecleado));

    [Fact]
    public void Exigiendo_rif_una_cedula_no_basta()
    {
        Assert.Null(Rif.Error("V-12345678"));
        Assert.NotNull(Rif.Error("V-12345678", exigirRif: true));
        Assert.Null(Rif.Error("J-00002961-0", exigirRif: true));
        Assert.Contains("dígito verificador", Rif.Error("J-00002961-5")!);
    }

    [Fact]
    public void Una_cedula_es_valida_pero_no_es_rif()
    {
        Assert.True(Rif.EsValido("V-12345678"));
        Assert.False(Rif.EsRifValido("V-12345678"));
    }
}
