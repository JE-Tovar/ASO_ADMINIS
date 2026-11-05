using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Tests;

public class CalculoFiscalTests
{
    [Fact]
    public void Renglon_redondea_precio_monto_e_iva_a_dos_decimales()
    {
        var linea = new VentaLinea { Cantidad = 3, PrecioUnitarioUsd = 1.111m, Alicuota = TipoAlicuota.General };

        CalculoFiscal.CalcularLinea(linea, Escenario.Organizacion(), tasa: 36.55m);

        Assert.Equal(40.61m, linea.PrecioUnitario);   // 1,111 × 36,55 = 40,606…
        Assert.Equal(121.83m, linea.Monto);           // 3 × 40,61
        Assert.Equal(19.49m, linea.Iva);              // 16 % de 121,83 = 19,4928
    }

    [Fact]
    public void Igtf_solo_sobre_lo_cobrado_en_divisas_y_con_tope_en_el_total()
    {
        var org = Escenario.Organizacion();
        var venta = new Venta
        {
            Lineas = [new VentaLinea { Cantidad = 1, PrecioUnitario = 100m, Alicuota = TipoAlicuota.Exento }],
            // Paga 150 Bs en divisas por una cuenta de 100: el IGTF no pasa de 3 % de 100.
            Pagos = [new VentaPago { Metodo = MetodoPago.EfectivoDivisa, MontoBs = 150m }]
        };
        CalculoFiscal.CalcularLinea(venta.Lineas[0], org, tasa: 1m, precioEnUsd: false);

        CalculoFiscal.Totalizar(venta, org, percibeIgtf: true);

        Assert.Equal(100m, venta.BaseIgtf);
        Assert.Equal(3m, venta.Igtf);
        Assert.Equal(103m, venta.Total);
        Assert.Equal(47m, venta.Vuelto);
    }

    [Fact]
    public void Sin_contribuyente_especial_no_hay_igtf()
    {
        var org = Escenario.Organizacion();
        var venta = new Venta
        {
            Lineas = [new VentaLinea { Cantidad = 1, PrecioUnitario = 100m, Alicuota = TipoAlicuota.Exento }],
            Pagos = [new VentaPago { Metodo = MetodoPago.Zelle, MontoBs = 100m }]
        };
        CalculoFiscal.CalcularLinea(venta.Lineas[0], org, tasa: 1m, precioEnUsd: false);

        CalculoFiscal.Totalizar(venta, org, percibeIgtf: false);

        Assert.Equal(0m, venta.Igtf);
        Assert.Equal(100m, venta.Total);
    }
}
