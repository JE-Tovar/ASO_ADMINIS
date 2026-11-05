using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Tests;

public class VentasServiceTests
{
    [Fact]
    public void Dos_facturas_de_contingencia_seguidas_reciben_numeros_internos_distintos()
    {
        var ventas = new VentasEnMemoria();
        var servicio = Escenario.Servicio(ventas);

        var primera = servicio.RegistrarEnContingencia(Escenario.Borrador(), "000101", "00-000101", usuarioId: 1);
        var segunda = servicio.RegistrarEnContingencia(Escenario.Borrador(), "000102", "00-000102", usuarioId: 1);

        Assert.Equal("VTA-000001", primera.Numero);
        Assert.Equal("VTA-000002", segunda.Numero);
        Assert.All([primera, segunda], v => Assert.Equal(EstadoVenta.PendienteTransmitir, v.Estado));
    }

    [Fact]
    public void Si_otra_caja_toma_el_numero_se_reintenta_con_el_siguiente()
    {
        var ventas = new VentasEnMemoria { RivalEnElPrimerAdd = true };
        var servicio = Escenario.Servicio(ventas);

        var factura = servicio.EmitirFactura(Escenario.Borrador(), usuarioId: 1);

        Assert.Equal("VTA-000002", factura.Numero);
        Assert.Equal(2, ventas.GetAll().Count());
    }

    [Fact]
    public void Factura_emitida_queda_con_numero_fiscal_y_control_del_emisor()
    {
        var ventas = new VentasEnMemoria();
        var servicio = Escenario.Servicio(ventas);

        var factura = servicio.EmitirFactura(Escenario.Borrador(), usuarioId: 1);

        Assert.Equal("F00000001", factura.NumeroFiscal);
        Assert.Equal("00-00000001", factura.NumeroControl);
        Assert.Equal(400m, factura.Total);
    }
}
