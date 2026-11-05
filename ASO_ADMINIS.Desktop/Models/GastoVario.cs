namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Moneda en que se pagó un gasto vario.
///
/// <b>Los miembros se añaden SIEMPRE al final:</b> se persiste como <c>int</c>.
/// </summary>
public enum MonedaGasto
{
    Bs,
    Usd
}

/// <summary>
/// Estado de un gasto vario. No hay borrador: nace registrado y se corrige anulándolo.
///
/// <b>Los miembros se añaden SIEMPRE al final:</b> se persiste como <c>int</c>.
/// </summary>
public enum EstadoGastoVario
{
    Registrado,
    Anulado
}

/// <summary>
/// Finanzas · Gastos varios: un pago que NO es a un proveedor con factura de compra — luz,
/// alquiler, transporte, limpieza, un técnico… Documento plano, sin líneas.
///
/// <b>Montos congelados.</b> Si se pagó en US$, al registrar se guarda la tasa BCV vigente y el
/// equivalente en Bs (<see cref="MontoBs"/>): el reporte suma en Bs y no puede moverse cuando
/// cambie la tasa. Mismo criterio que la tasa guardada en cada <see cref="Venta"/>.
///
/// <b>Cuenta opcional.</b> Si se pagó desde una cuenta de Banco (incluida la caja chica o la de
/// divisas), el servicio escribe su asiento con <see cref="OrigenMovimiento.GastoVario"/>; si
/// <see cref="CuentaId"/> es nulo fue efectivo por fuera de las cuentas y Banco no se entera.
/// </summary>
public class GastoVario : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Estampada por <c>AsoAdminisDbContext</c>; ver <see cref="IDeOrganizacion"/>.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>Correlativo GAS-000123, asignado por el servicio al registrar.</summary>
    public string Numero { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    /// <summary>Agrupación libre (Servicios públicos, Alquiler…), sugerida pero editable.</summary>
    public string Categoria { get; set; } = string.Empty;

    /// <summary>En qué consistió: "Recibo de luz de octubre".</summary>
    public string Concepto { get; set; } = string.Empty;

    /// <summary>A quién se le pagó (opcional): "Corpoelec", "Sr. José, flete".</summary>
    public string Beneficiario { get; set; } = string.Empty;

    /// <summary>N° de recibo, transferencia o comprobante (opcional).</summary>
    public string Referencia { get; set; } = string.Empty;

    public MonedaGasto Moneda { get; set; }

    /// <summary>Monto en la moneda del pago.</summary>
    public decimal Monto { get; set; }

    /// <summary>Tasa BCV usada para pasar a Bs (1 si se pagó en Bs). Snapshot.</summary>
    public decimal TasaCambio { get; set; } = 1m;

    /// <summary>Equivalente en Bs con la tasa del registro. Snapshot; es lo que suma el reporte.</summary>
    public decimal MontoBs { get; set; }

    /// <summary>Cuenta de Banco de la que salió el dinero; nulo = efectivo fuera de las cuentas.</summary>
    public int? CuentaId { get; set; }
    public string CuentaNombre { get; set; } = string.Empty;  // snapshot

    public EstadoGastoVario Estado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    public int CreadoPorId { get; set; }
    public string CreadoPorNombre { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }

    public bool Vigente => Estado == EstadoGastoVario.Registrado;

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string MonedaTexto => TextoDe(Moneda);
    public string MontoTexto => $"{MonedaTexto} {Monto:N2}";
    public string MontoBsTexto => MontoBs.ToString("N2");
    public string PagoTexto => CuentaId is null ? "Efectivo" : CuentaNombre;
    public string EstadoTexto => Estado == EstadoGastoVario.Registrado ? "Registrado" : "Anulado";

    public static string TextoDe(MonedaGasto moneda) => moneda == MonedaGasto.Usd ? "US$" : "Bs";

    /// <summary>
    /// La moneda de una <see cref="CuentaBancaria"/> es texto ("Bs", "USD"); esto dice si
    /// corresponde a la del gasto.
    /// </summary>
    public static bool MonedaCoincide(MonedaGasto moneda, string monedaCuenta)
    {
        var texto = (monedaCuenta ?? string.Empty).Trim().ToUpperInvariant();
        var esDolar = texto is "USD" or "US$" or "$" or "DOLARES" or "DÓLARES";
        return moneda == MonedaGasto.Usd ? esDolar : !esDolar;
    }

    public GastoVario Clonar() => (GastoVario)MemberwiseClone();
}
