using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Tests;

/// <summary>Finanzas · Gastos varios y Reportes · Gastos, con todo en memoria.</summary>
public class GastosTests
{
    private readonly GastosEnMemoria _gastos = new();
    private readonly CuentasEnMemoria _cuentas = new();
    private readonly MovimientosEnMemoria _movimientos = new();
    private readonly TasasEnMemoria _tasas = new();
    private readonly FacturasProveedorEnMemoria _facturas = new();

    private readonly CuentaBancaria _cuentaBs;
    private readonly CuentaBancaria _cuentaUsd;

    public GastosTests()
    {
        _cuentaBs = _cuentas.Add(new CuentaBancaria { Nombre = "Banco Bs", Moneda = "Bs" });
        _cuentaUsd = _cuentas.Add(new CuentaBancaria { Nombre = "Caja divisas", Moneda = "USD" });
        _tasas.Add(new TasaCambio { Fecha = DateTime.Today.AddDays(-10), Valor = 40m });
        _tasas.Add(new TasaCambio { Fecha = DateTime.Today.AddDays(-2), Valor = 45m });
    }

    private GastosVariosService Servicio() =>
        new(_gastos, new BancoService(_movimientos, _cuentas, new SesionTotal()), _tasas, new SesionTotal());

    private static GastoVario Borrador(MonedaGasto moneda, decimal monto, int? cuentaId = null, int diasAtras = 0) => new()
    {
        Fecha = DateTime.Today.AddDays(-diasAtras),
        Categoria = "Servicios públicos",
        Concepto = "Recibo de luz de octubre",
        Moneda = moneda,
        Monto = monto,
        CuentaId = cuentaId
    };

    [Fact]
    public void Gasto_en_dolares_congela_la_tasa_de_su_fecha_y_el_monto_en_bs()
    {
        var gasto = Servicio().Registrar(Borrador(MonedaGasto.Usd, 10m, diasAtras: 5), usuarioId: 1);

        Assert.Equal("GAS-000001", gasto.Numero);
        Assert.Equal(40m, gasto.TasaCambio);   // la vigente hace 5 días, no la última
        Assert.Equal(400m, gasto.MontoBs);
    }

    [Fact]
    public void Gasto_en_efectivo_no_toca_el_banco()
    {
        Servicio().Registrar(Borrador(MonedaGasto.Bs, 150m), usuarioId: 1);

        Assert.Empty(_movimientos.GetAll());
    }

    [Fact]
    public void Gasto_desde_una_cuenta_crea_su_asiento_y_anularlo_lo_anula()
    {
        var servicio = Servicio();
        var gasto = servicio.Registrar(Borrador(MonedaGasto.Usd, 20m, _cuentaUsd.Id), usuarioId: 1);

        var asiento = Assert.Single(_movimientos.GetAll());
        Assert.Equal(OrigenMovimiento.GastoVario, asiento.Origen);
        Assert.Equal(gasto.Id, asiento.OrigenId);
        Assert.Equal(20m, asiento.Monto);
        Assert.Equal(TipoMovimientoBanco.Salida, asiento.Tipo);

        servicio.Anular(gasto, "Cargado dos veces");

        Assert.Equal(EstadoGastoVario.Anulado, _gastos.GetById(gasto.Id)!.Estado);
        Assert.Equal(EstadoMovimientoBanco.Anulado, Assert.Single(_movimientos.GetAll()).Estado);
    }

    [Fact]
    public void Una_cuenta_de_otra_moneda_se_rechaza()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Servicio().Registrar(Borrador(MonedaGasto.Usd, 20m, _cuentaBs.Id), usuarioId: 1));

        Assert.Contains("no es en US$", ex.Message);
        Assert.Empty(_gastos.GetAll());
    }

    [Fact]
    public void Si_el_asiento_falla_se_retira_el_gasto()
    {
        _movimientos.Fallar = true;

        Assert.Throws<InvalidOperationException>(() =>
            Servicio().Registrar(Borrador(MonedaGasto.Bs, 50m, _cuentaBs.Id), usuarioId: 1));

        Assert.Empty(_gastos.GetAll());
    }

    [Fact]
    public void El_reporte_suma_proveedores_y_gastos_varios_en_bs()
    {
        var servicio = Servicio();
        servicio.Registrar(Borrador(MonedaGasto.Bs, 100m), usuarioId: 1);
        servicio.Registrar(Borrador(MonedaGasto.Usd, 10m, diasAtras: 1), usuarioId: 1);     // 450 Bs
        var anulado = servicio.Registrar(Borrador(MonedaGasto.Bs, 999m), usuarioId: 1);
        servicio.Anular(anulado, "Error");

        // Factura pagada desde la cuenta en US$ hace 1 día: 30 US$ × 45 = 1.350 Bs.
        var factura = _facturas.Add(new FacturaProveedor
        {
            ProveedorNombre = "Pinturas, C.A.",
            Monto = 30m,
            Estado = EstadoFacturaProveedor.Pagada,
            FechaPago = DateTime.Today.AddDays(-1)
        });
        _movimientos.Add(new MovimientoBanco
        {
            CuentaId = _cuentaUsd.Id,
            Origen = OrigenMovimiento.FacturaProveedor,
            OrigenId = factura.Id,
            Monto = 30m
        });

        // Una factura pendiente es deuda, no gasto.
        _facturas.Add(new FacturaProveedor { Monto = 500m, Estado = EstadoFacturaProveedor.Pendiente });

        var filas = new ReporteGastosService(_facturas, _gastos, _movimientos, _cuentas, _tasas)
            .Filas(DateTime.Today.AddDays(-30), DateTime.Today);

        Assert.Equal(3, filas.Count);
        Assert.Equal(1350m, filas.Single(f => f.Origen == OrigenGasto.Proveedor).MontoBs);
        Assert.Equal(550m, filas.Where(f => f.Origen == OrigenGasto.GastoVario).Sum(f => f.MontoBs));
    }
}
