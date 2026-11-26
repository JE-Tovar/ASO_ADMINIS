using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Tests;

public class CuentasPorCobrarTests
{
    private static Venta Credito(decimal anticipo = 0)
    {
        var borrador = Escenario.Borrador();
        borrador.CondicionPago = CondicionVenta.Credito;
        borrador.FechaVencimientoCredito = DateTime.Today.AddDays(15);
        borrador.Pagos = anticipo > 0
            ? [new VentaPago { Metodo = MetodoPago.EfectivoBs, MontoBs = anticipo }]
            : [];
        return borrador;
    }

    private static CobroCliente CobroBs(decimal monto, MetodoPago metodo = MetodoPago.EfectivoBs, int? cuentaId = null) => new()
    {
        Fecha = DateTime.Today, Metodo = metodo, MontoBs = monto, CuentaBancariaId = cuentaId
    };

    [Fact]
    public void Contado_no_genera_cuenta_por_cobrar()
    {
        var m = Escenario.Montar(new VentasEnMemoria());

        var factura = m.Ventas.EmitirFactura(Escenario.Borrador(), 1);

        Assert.Equal(CondicionVenta.Contado, factura.CondicionPago);
        Assert.Null(m.CuentasPorCobrar.DeLaFactura(factura.Id));
        Assert.Empty(m.CuentasPorCobrarFuente.GetAll());
    }

    [Fact]
    public void Credito_total_crea_deuda_en_bs_y_referencia_a_tasa_de_emision()
    {
        var m = Escenario.Montar(new VentasEnMemoria());

        var factura = m.Ventas.EmitirFactura(Credito(), 1);
        var cuenta = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;

        Assert.NotNull(factura.ClienteId);
        Assert.Equal(400m, cuenta.SaldoBs);
        Assert.Equal(10m, cuenta.SaldoUsdReferencia);
        Assert.Equal(0m, cuenta.PagoInicialBs);
        Assert.Equal(DateTime.Today.AddDays(15), cuenta.FechaVencimiento);
        Assert.Equal(TipoMovimientoCuentaPorCobrar.Apertura,
            Assert.Single(m.CuentasPorCobrar.Movimientos(cuenta.Id)).Tipo);
        Assert.Empty(factura.Pagos);
    }

    [Fact]
    public void Credito_con_anticipo_cobra_solo_el_saldo_sin_alterar_la_factura()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var factura = m.Ventas.EmitirFactura(Credito(150m), 1);
        var cuenta = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;

        var recibo = m.CuentasPorCobrar.RegistrarCobro(cuenta, CobroBs(100m), 1);

        Assert.Equal(250m, cuenta.SaldoBs);
        Assert.Equal(150m, m.CuentasPorCobrar.DeLaFactura(factura.Id)!.SaldoBs);
        Assert.Equal("REC-000001", recibo.Numero);
        Assert.Equal(m.Turnos.TurnoAbierto()!.Id, recibo.TurnoId);
        Assert.Equal(250m, m.Turnos.Esperado(m.Turnos.TurnoAbierto()!).Bs);
        Assert.Equal(150m, Assert.Single(factura.Pagos).MontoBs);
    }

    [Fact]
    public void Abonos_parciales_y_total_cambian_estado_y_correlativo()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var factura = m.Ventas.EmitirFactura(Credito(), 1);
        var cuenta = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;

        var primero = m.CuentasPorCobrar.RegistrarCobro(cuenta, CobroBs(125m), 1);
        var segundo = m.CuentasPorCobrar.RegistrarCobro(cuenta, CobroBs(275m), 1);
        var actual = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;

        Assert.Equal("REC-000002", segundo.Numero);
        Assert.NotEqual(primero.Id, segundo.Id);
        Assert.Equal(0m, actual.SaldoBs);
        Assert.Equal(EstadoCuentaPorCobrar.Cobrada, actual.Estado);
        Assert.Equal(2, m.CuentasPorCobrar.Cobros(cuenta.Id).Count);
        Assert.Equal(3, m.CuentasPorCobrar.Movimientos(cuenta.Id).Count);
    }

    [Fact]
    public void Sobrepago_y_cobro_de_cuenta_ya_saldada_se_rechazan()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var factura = m.Ventas.EmitirFactura(Credito(), 1);
        var cuenta = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;

        Assert.Throws<InvalidOperationException>(() =>
            m.CuentasPorCobrar.RegistrarCobro(cuenta, CobroBs(400.01m), 1));
        m.CuentasPorCobrar.RegistrarCobro(cuenta, CobroBs(400m), 1);

        Assert.Throws<InvalidOperationException>(() =>
            m.CuentasPorCobrar.RegistrarCobro(cuenta, CobroBs(1m), 1));
        Assert.Single(m.CobrosFuente.GetAll());
    }

    [Fact]
    public void Credito_exige_permiso_y_vencimiento()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        m.Sesion.Negados.Add(Permisos.Ventas.VenderACredito);
        Assert.Throws<InvalidOperationException>(() => m.Ventas.EmitirFactura(Credito(), 1));
        Assert.Empty(m.CuentasPorCobrarFuente.GetAll());

        m.Sesion.Negados.Clear();
        var sinVencimiento = Credito();
        sinVencimiento.FechaVencimientoCredito = null;
        Assert.Throws<InvalidOperationException>(() => m.Ventas.EmitirFactura(sinVencimiento, 1));
    }

    [Fact]
    public void Credito_no_admite_sobrepago_en_los_pagos_iniciales()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        Assert.Throws<InvalidOperationException>(() => m.Ventas.EmitirFactura(Credito(450m), 1));
        Assert.Empty(m.CuentasPorCobrarFuente.GetAll());
    }

    [Fact]
    public void Cobro_en_efectivo_exige_turno_actual_propio()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var factura = m.Ventas.EmitirFactura(Credito(), 1);
        var cuenta = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;
        m.Turnos.Cerrar(m.Turnos.TurnoAbierto()!, 0, 0, "");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            m.CuentasPorCobrar.RegistrarCobro(cuenta, CobroBs(100m), 1));

        Assert.Contains("turno abierto", ex.Message);
        Assert.Empty(m.CobrosFuente.GetAll());
    }

    [Fact]
    public void Cobro_bancario_asienta_por_recibo_y_anularlo_reversa_ambos()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var bancaria = m.CuentasBancariasFuente.Add(new CuentaBancaria
        {
            Nombre = "Banco Bs", Moneda = "Bs", Activa = true, FechaApertura = DateTime.Today.AddYears(-1)
        });
        var factura = m.Ventas.EmitirFactura(Credito(), 1);
        var cuenta = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;
        var recibo = m.CuentasPorCobrar.RegistrarCobro(cuenta,
            CobroBs(200m, MetodoPago.Transferencia, bancaria.Id), 1);

        var asiento = Assert.Single(m.MovimientosBancoFuente.GetAll());
        Assert.Equal(TipoMovimientoBanco.Entrada, asiento.Tipo);
        Assert.Equal(200m, asiento.Monto);
        Assert.Equal(OrigenMovimiento.CobroCliente, asiento.Origen);
        Assert.Equal(recibo.Id, asiento.OrigenId);

        m.CuentasPorCobrar.AnularCobro(recibo, "Transferencia devuelta", 1);

        Assert.Equal(400m, m.CuentasPorCobrar.DeLaFactura(factura.Id)!.SaldoBs);
        Assert.False(m.CobrosFuente.GetById(recibo.Id)!.Vigente);
        Assert.Equal(EstadoMovimientoBanco.Anulado, m.MovimientosBancoFuente.GetById(asiento.Id)!.Estado);
        Assert.Contains(m.CuentasPorCobrar.Movimientos(cuenta.Id),
            mov => mov.Tipo == TipoMovimientoCuentaPorCobrar.AnulacionCobro);
    }

    [Fact]
    public void No_se_anula_un_recibo_conciliado_o_de_turno_cerrado()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var factura = m.Ventas.EmitirFactura(Credito(), 1);
        var cuenta = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;
        var recibo = m.CuentasPorCobrar.RegistrarCobro(cuenta, CobroBs(100m), 1);
        m.Turnos.Cerrar(m.Turnos.TurnoAbierto()!, 100m, 0, "");

        Assert.Throws<InvalidOperationException>(() =>
            m.CuentasPorCobrar.AnularCobro(recibo, "Error", 1));
        Assert.True(m.CobrosFuente.GetById(recibo.Id)!.Vigente);
    }

    [Fact]
    public void Contribuyente_especial_bloquea_cobro_posterior_en_divisa()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        m.OrganizacionActual.ContribuyenteEspecial = true;
        var factura = m.Ventas.EmitirFactura(Credito(), 1);
        var cuenta = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;

        var ex = Assert.Throws<InvalidOperationException>(() => m.CuentasPorCobrar.RegistrarCobro(cuenta,
            new CobroCliente { Fecha = DateTime.Today, Metodo = MetodoPago.EfectivoDivisa, MontoDivisa = 2m }, 1));

        Assert.Contains("IGTF", ex.Message);
        Assert.Empty(m.CobrosFuente.GetAll());
    }

    [Fact]
    public void Cobro_en_divisa_convierte_a_bs_y_conserva_el_monto_recibido()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var factura = m.Ventas.EmitirFactura(Credito(), 1);
        var cuenta = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;

        var recibo = m.CuentasPorCobrar.RegistrarCobro(cuenta,
            new CobroCliente { Fecha = DateTime.Today, Metodo = MetodoPago.EfectivoDivisa, MontoDivisa = 10m }, 1);

        Assert.Equal(400m, recibo.MontoBs);
        Assert.Equal(10m, recibo.MontoDivisa);
        Assert.Equal(40m, recibo.TasaCambio);
        Assert.Equal(EstadoCuentaPorCobrar.Cobrada, m.CuentasPorCobrar.DeLaFactura(factura.Id)!.Estado);
        Assert.Equal(10m, m.Turnos.Esperado(m.Turnos.TurnoAbierto()!).Divisa);
    }

    [Fact]
    public void Cobro_en_divisa_redondeado_cierra_saldo_sin_sobrepago()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var factura = m.Ventas.EmitirFactura(Credito(), 1);
        var cuenta = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;
        m.TasasFuente.GetAll().Single().Valor = 36.58m;
        var editor = new ASO_ADMINIS.Desktop.ViewModels.CobroClienteEditorViewModel(cuenta, m.CuentasPorCobrar)
        {
            MetodoSeleccionado = ASO_ADMINIS.Desktop.ViewModels.PagoVentaEditorViewModel.Metodos
                .Single(metodo => metodo.Valor == MetodoPago.EfectivoDivisa)
        };
        editor.CobrarTodoCommand.Execute(null);
        var datos = editor.ObtenerResultado();

        var recibo = m.CuentasPorCobrar.RegistrarCobro(cuenta, datos, 1);

        Assert.Equal(10.93m, recibo.MontoDivisa);
        Assert.Equal(400m, recibo.MontoBs);
        Assert.Equal(EstadoCuentaPorCobrar.Cobrada, m.CuentasPorCobrar.DeLaFactura(factura.Id)!.Estado);
    }

    [Fact]
    public void Nota_de_credito_posterior_al_cobro_total_deja_saldo_a_favor()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var factura = m.Ventas.EmitirFactura(Credito(), 1);
        var cuenta = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;
        m.CuentasPorCobrar.RegistrarCobro(cuenta, CobroBs(400m), 1);

        m.Ventas.EmitirNotaCredito(factura, [(factura.Lineas[0], 1m)], "Servicio no realizado", 1);
        var actual = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;

        Assert.Equal(-400m, actual.SaldoBs);
        Assert.Equal(EstadoCuentaPorCobrar.SaldoAFavor, actual.Estado);
        Assert.Contains(m.CuentasPorCobrar.Movimientos(cuenta.Id),
            mov => mov.Tipo == TipoMovimientoCuentaPorCobrar.NotaCredito && mov.EfectoBs == -400m);
    }

    [Fact]
    public void Nota_de_debito_aumenta_la_deuda()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var factura = m.Ventas.EmitirFactura(Credito(), 1);
        var cuenta = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;

        m.Ventas.EmitirNotaDebito(factura, [("Cargo administrativo", 50m, TipoAlicuota.Exento)], "Acordado", 1);

        Assert.Equal(450m, m.CuentasPorCobrar.DeLaFactura(factura.Id)!.SaldoBs);
        Assert.Contains(m.CuentasPorCobrar.Movimientos(cuenta.Id),
            mov => mov.Tipo == TipoMovimientoCuentaPorCobrar.NotaDebito && mov.EfectoBs == 50m);
    }

    [Fact]
    public void Retencion_reduce_saldo_sin_crear_cobro_ni_asiento_bancario()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var borrador = Credito();
        borrador.ClienteRif = "J-00002961-0";
        borrador.Lineas = [Escenario.Renglon(Escenario.AceiteId, "L", 2m)];
        var factura = m.Ventas.EmitirFactura(borrador, 1);
        var fuente = new RetencionesEnMemoria();
        var retenciones = new RetencionesService(fuente, m.Sesion, m.CuentasPorCobrar);

        retenciones.Registrar(factura, "20261100000001", DateTime.Today, 75m, 1);
        var cuenta = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;

        Assert.Equal(464m, factura.Total);
        Assert.Equal(416m, cuenta.SaldoBs);
        Assert.Single(fuente.GetAll());
        Assert.Empty(m.CobrosFuente.GetAll());
        Assert.Empty(m.MovimientosBancoFuente.GetAll());
    }

    [Fact]
    public void Fallo_al_guardar_movimiento_revierte_recibo_y_saldo()
    {
        var m = Escenario.Montar(new VentasEnMemoria());
        var factura = m.Ventas.EmitirFactura(Credito(), 1);
        var cuenta = m.CuentasPorCobrar.DeLaFactura(factura.Id)!;
        m.MovimientosCxcFuente.Fallar = true;
        using var unidad = UnidadDeTrabajo.Usar(new TransaccionEnMemoria(
            m.CobrosFuente, m.CuentasPorCobrarFuente, m.MovimientosCxcFuente));

        Assert.Throws<InvalidOperationException>(() =>
            m.CuentasPorCobrar.RegistrarCobro(cuenta, CobroBs(100m), 1));

        Assert.Empty(m.CobrosFuente.GetAll());
        Assert.Equal(400m, m.CuentasPorCobrarFuente.GetById(cuenta.Id)!.SaldoBs);
        Assert.Single(m.MovimientosCxcFuente.GetAll());
    }

    [Fact]
    public void Vencida_es_derivada_y_los_permisos_son_de_supervision()
    {
        var cuenta = new CuentaPorCobrar
        {
            Estado = EstadoCuentaPorCobrar.Pendiente, SaldoBs = 100m, FechaVencimiento = DateTime.Today.AddDays(-1)
        };

        Assert.True(cuenta.EstaVencida);
        Assert.Equal("Vencida", cuenta.EstadoTexto);
        var supervisor = MatrizPermisos.Base(Rol.Supervisor);
        Assert.Contains(Permisos.CuentasPorCobrar.Cobrar, supervisor);
        Assert.Contains(Permisos.CuentasPorCobrar.AnularCobro, supervisor);
        Assert.DoesNotContain(Permisos.Ventas.VenderACredito, MatrizPermisos.Base(Rol.Cajero));
    }
}
