using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>Una fila de Reportes · Gastos: un pago a proveedor o un gasto vario, en Bs.</summary>
public sealed record FilaGasto(
    DateTime Fecha,
    OrigenGasto Origen,
    string Categoria,
    string Beneficiario,
    string Documento,
    string Concepto,
    string Moneda,
    decimal Monto,
    decimal MontoBs)
{
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string OrigenTexto => Origen == OrigenGasto.Proveedor ? "Proveedor" : "Gasto vario";
    public string MontoTexto => $"{Moneda} {Monto:N2}";
    public string MontoBsTexto => MontoBs.ToString("N2");
}

public enum OrigenGasto
{
    Proveedor,
    GastoVario
}

/// <summary>
/// Reportes · Gastos: junta en una sola lista, y en Bs, lo que la empresa pagó.
///
/// - <b>Proveedores:</b> facturas pagadas, por su fecha de pago (como antes). Su moneda es la de
///   la cuenta de la que salió el pago (el asiento en Banco); si fue una cuenta en US$, se pasa a
///   Bs con la tasa BCV vigente ese día. Sin asiento (datos viejos) se toma como Bs.
/// - <b>Gastos varios:</b> los vigentes, con el <see cref="GastoVario.MontoBs"/> que se congeló
///   al registrarlos.
///
/// "Gasto" es dinero que salió, no una deuda: una factura todavía pendiente no entra.
/// </summary>
public sealed class ReporteGastosService(IFacturaProveedorDataSource facturas,
                                         IGastoVarioDataSource gastos,
                                         IMovimientoBancoDataSource movimientos,
                                         ICuentaBancariaDataSource cuentas,
                                         ITasaCambioDataSource tasas)
{
    public const string CategoriaProveedores = "Compras a proveedores";

    public IReadOnlyList<FilaGasto> Filas(DateTime? desde, DateTime? hasta)
    {
        bool EnRango(DateTime fecha) =>
            (desde is not { } d || fecha.Date >= d.Date) && (hasta is not { } h || fecha.Date <= h.Date);

        var listaTasas = tasas.GetAll().ToList();
        var monedaCuenta = cuentas.GetAll().ToDictionary(c => c.Id, c => c.Moneda);
        var asientosProveedor = movimientos.GetAll()
            .Where(m => m.Origen == OrigenMovimiento.FacturaProveedor && m.OrigenId is not null
                        && m.Estado != EstadoMovimientoBanco.Anulado)
            .GroupBy(m => m.OrigenId!.Value)
            .ToDictionary(g => g.Key, g => g.First().CuentaId);

        var deProveedores = facturas.GetAll()
            .Where(f => f.Estado == EstadoFacturaProveedor.Pagada && f.FechaPago is { } p && EnRango(p))
            .Select(f =>
            {
                var fecha = f.FechaPago!.Value;
                var enDolares = asientosProveedor.TryGetValue(f.Id, out var cuentaId)
                                && monedaCuenta.TryGetValue(cuentaId, out var moneda)
                                && GastoVario.MonedaCoincide(MonedaGasto.Usd, moneda);
                var montoBs = enDolares
                    ? CalculoFiscal.Redondear(f.Monto * GastosVariosService.TasaEn(listaTasas, fecha))
                    : f.Monto;

                return new FilaGasto(fecha, OrigenGasto.Proveedor, CategoriaProveedores, f.ProveedorNombre,
                                     f.NumeroDocumento, f.Descripcion,
                                     enDolares ? "US$" : "Bs", f.Monto, montoBs);
            });

        var varios = gastos.GetAll()
            .Where(g => g.Vigente && EnRango(g.Fecha))
            .Select(g => new FilaGasto(g.Fecha, OrigenGasto.GastoVario, g.Categoria, g.Beneficiario,
                                       g.Numero, g.Concepto, g.MonedaTexto, g.Monto, g.MontoBs));

        return [.. deProveedores.Concat(varios).OrderBy(f => f.Fecha)];
    }
}
