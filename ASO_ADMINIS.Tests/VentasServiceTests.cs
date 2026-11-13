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

    // ---------------------------------------------------------------- Precio fijo

    [Fact]
    public void El_precio_de_la_factura_es_el_del_catalogo_aunque_la_pantalla_mande_otro()
    {
        var servicio = Escenario.Servicio(new VentasEnMemoria());
        var borrador = Escenario.Borrador();
        borrador.Lineas[0].PrecioUnitarioUsd = 1m;     // la pantalla intenta cobrar 1 US$
        borrador.Lineas[0].Alicuota = TipoAlicuota.General;

        var factura = servicio.EmitirFactura(borrador, usuarioId: 1);

        var linea = Assert.Single(factura.Lineas);
        Assert.Equal(10m, linea.PrecioUnitarioUsd);
        Assert.Equal(TipoAlicuota.Exento, linea.Alicuota);
        Assert.Equal("Mezcla de pintura a la medida", linea.Descripcion);
        Assert.Equal(400m, factura.Total);
    }

    [Fact]
    public void Un_articulo_sin_precio_no_se_factura()
    {
        var montaje = Escenario.Montar(new VentasEnMemoria());
        montaje.Articulos.GetById(Escenario.AceiteId)!.PrecioVenta = 0m;
        var borrador = Escenario.Borrador();
        borrador.Lineas = [Escenario.Renglon(Escenario.AceiteId, "L", 1)];

        var ex = Assert.Throws<InvalidOperationException>(() => montaje.Ventas.EmitirFactura(borrador, usuarioId: 1));
        Assert.Contains("no tiene precio", ex.Message);
    }

    [Fact]
    public void Un_renglon_que_no_esta_en_el_catalogo_no_se_factura()
    {
        var servicio = Escenario.Servicio(new VentasEnMemoria());
        var borrador = Escenario.Borrador();
        borrador.Lineas[0].ArticuloId = null;

        Assert.Throws<InvalidOperationException>(() => servicio.EmitirFactura(borrador, usuarioId: 1));
    }

    // ---------------------------------------------------------------- Servicios

    [Fact]
    public void Un_servicio_se_factura_sin_existencia_y_no_sale_del_almacen()
    {
        var montaje = Escenario.Montar(new VentasEnMemoria());

        var factura = montaje.Ventas.EmitirFactura(Escenario.Borrador(), usuarioId: 1);

        Assert.True(Assert.Single(factura.Lineas).EsServicio);
        Assert.Empty(montaje.Salidas.GetAll());
    }

    [Fact]
    public void Factura_mixta_descuenta_solo_el_bien()
    {
        var montaje = Escenario.Montar(new VentasEnMemoria());
        var borrador = Escenario.Borrador();
        // 1 servicio exento (10 US$ = Bs 400) + 2 L de aceite en la general (2 × 5 US$ = Bs 400 + 16 % = 464).
        borrador.Lineas = [Escenario.Renglon(Escenario.ServicioMezclaId, "serv", 1), Escenario.Renglon(Escenario.AceiteId, "L", 2)];
        borrador.Pagos = [new VentaPago { Metodo = MetodoPago.EfectivoBs, MontoBs = 864m }];

        var factura = montaje.Ventas.EmitirFactura(borrador, usuarioId: 1);

        Assert.Equal(864m, factura.Total);
        var salida = Assert.Single(montaje.Salidas.GetAll());
        var linea = Assert.Single(salida.Lineas);
        Assert.Equal(Escenario.AceiteId, linea.ArticuloId);
        Assert.Equal(2m, linea.Cantidad);
    }

    [Fact]
    public void La_nota_de_credito_de_un_servicio_no_reingresa_nada_al_almacen()
    {
        var montaje = Escenario.Montar(new VentasEnMemoria());
        var factura = montaje.Ventas.EmitirFactura(Escenario.Borrador(), usuarioId: 1);
        var entradasAntes = montaje.Entradas.GetAll().Count();

        var nota = montaje.Ventas.EmitirNotaCredito(factura, [(factura.Lineas[0], 1m)], "Servicio mal hecho", usuarioId: 1);

        Assert.Equal(400m, nota.Total);
        Assert.Equal(entradasAntes, montaje.Entradas.GetAll().Count());
    }

    // ---------------------------------------------------------------- Granel

    [Fact]
    public void A_granel_se_factura_con_3_decimales_y_el_almacen_descuenta_lo_mismo()
    {
        var montaje = Escenario.Montar(new VentasEnMemoria());
        var borrador = Escenario.Borrador();
        // 0,125 L de aceite a 5 US$ = 0,625 US$ → Bs 25,00 + 16 % = 29,00.
        borrador.Lineas = [Escenario.Renglon(Escenario.AceiteId, "L", 0.125m)];
        borrador.Pagos = [new VentaPago { Metodo = MetodoPago.EfectivoBs, MontoBs = 29m }];

        var factura = montaje.Ventas.EmitirFactura(borrador, usuarioId: 1);

        Assert.Equal(0.125m, Assert.Single(factura.Lineas).Cantidad);
        Assert.Equal(29m, factura.Total);
        Assert.Equal(0.125m, Assert.Single(Assert.Single(montaje.Salidas.GetAll()).Lineas).Cantidad);
    }

    [Fact]
    public void Mas_de_3_decimales_se_rechaza()
    {
        var montaje = Escenario.Montar(new VentasEnMemoria());
        var borrador = Escenario.Borrador();
        borrador.Lineas = [Escenario.Renglon(Escenario.AceiteId, "L", 0.1255m)];

        var ex = Assert.Throws<InvalidOperationException>(() => montaje.Ventas.EmitirFactura(borrador, usuarioId: 1));
        Assert.Contains("3 decimales", ex.Message);
    }

    [Fact]
    public void Lo_que_no_es_a_granel_solo_se_vende_en_cantidades_enteras()
    {
        var montaje = Escenario.Montar(new VentasEnMemoria());
        var borrador = Escenario.Borrador();
        borrador.Lineas = [Escenario.Renglon(Escenario.ServicioMezclaId, "serv", 1.5m)];

        var ex = Assert.Throws<InvalidOperationException>(() => montaje.Ventas.EmitirFactura(borrador, usuarioId: 1));
        Assert.Contains("unidad entera", ex.Message);
    }
}
