namespace ASO_ADMINIS.Desktop.Models;

/// <summary>Forma de pago acordada al emitir una factura. Se persiste como ordinal.</summary>
public enum CondicionVenta
{
    Contado,
    Credito
}

/// <summary>Estado financiero de la deuda. El vencimiento es una condición derivada de la fecha.</summary>
public enum EstadoCuentaPorCobrar
{
    Pendiente,
    Cobrada,
    SaldoAFavor
}

/// <summary>Ciclo de vida de un recibo de cobranza.</summary>
public enum EstadoCobroCliente
{
    Vigente,
    Anulado
}

/// <summary>Hechos que explican el saldo de una cuenta por cobrar.</summary>
public enum TipoMovimientoCuentaPorCobrar
{
    Apertura,
    Cobro,
    AnulacionCobro,
    RetencionIva,
    NotaCredito,
    NotaDebito
}

/// <summary>
/// Saldo administrativo de una factura de venta a crédito. Los importes son Bs; el equivalente
/// en US$ usa siempre la tasa de emisión y no cambia con las cobranzas posteriores.
/// </summary>
public class CuentaPorCobrar : IEntidad<int>, IDeOrganizacion, IConVersion
{
    public byte[]? Version { get; set; }
    public int OrganizacionId { get; set; }
    public int Id { get; set; }
    public int VentaId { get; set; }
    public int ClienteId { get; set; }
    public string NumeroFactura { get; set; } = string.Empty;
    public string ClienteRif { get; set; } = string.Empty;
    public string ClienteNombre { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public decimal TotalFacturaBs { get; set; }
    public decimal PagoInicialBs { get; set; }
    public decimal SaldoBs { get; set; }
    public decimal TasaCambioEmision { get; set; }
    public EstadoCuentaPorCobrar Estado { get; set; }
    public DateTime? FechaCobro { get; set; }
    public int CreadoPorId { get; set; }
    public DateTime FechaCreacion { get; set; }

    public decimal SaldoUsdReferencia => TasaCambioEmision > 0
        ? Math.Round(SaldoBs / TasaCambioEmision, 2)
        : 0m;

    public bool EstaVencida => Estado == EstadoCuentaPorCobrar.Pendiente
        && SaldoBs > 0
        && FechaVencimiento.Date < DateTime.Today;

    public int DiasParaVencer => (FechaVencimiento.Date - DateTime.Today).Days;
    public string EstadoTexto => EstaVencida ? "Vencida" : Estado switch
    {
        EstadoCuentaPorCobrar.Pendiente => "Pendiente",
        EstadoCuentaPorCobrar.Cobrada => "Cobrada",
        _ => "Saldo a favor"
    };
    public string ClienteTexto => $"{ClienteRif} · {ClienteNombre}";
    public string EmisionTexto => FechaEmision.ToString("dd/MM/yyyy");
    public string VencimientoTexto => FechaVencimiento.ToString("dd/MM/yyyy");
    public string TotalTexto => TotalFacturaBs.ToString("N2");
    public string SaldoTexto => SaldoBs.ToString("N2");
    public string SaldoUsdTexto => SaldoUsdReferencia.ToString("N2");
    public string PlazoTexto => Estado == EstadoCuentaPorCobrar.Pendiente
        ? EstaVencida ? $"Vencida hace {-DiasParaVencer} día(s)" : $"Vence en {DiasParaVencer} día(s)"
        : EstadoTexto;

    public CuentaPorCobrar Clonar() => (CuentaPorCobrar)MemberwiseClone();
}

/// <summary>
/// Recibo de una cobranza posterior a la factura. No modifica los pagos del documento fiscal.
/// </summary>
public class CobroCliente : IEntidad<int>, IDeOrganizacion, IConVersion
{
    public byte[]? Version { get; set; }
    public int OrganizacionId { get; set; }
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public int CuentaPorCobrarId { get; set; }
    public int VentaId { get; set; }
    public DateTime Fecha { get; set; }
    public MetodoPago Metodo { get; set; }
    public decimal MontoBs { get; set; }
    public decimal MontoDivisa { get; set; }
    public decimal TasaCambio { get; set; }
    public string Referencia { get; set; } = string.Empty;
    public int? CuentaBancariaId { get; set; }
    public string CuentaBancariaNombre { get; set; } = string.Empty;
    public int? CajaId { get; set; }
    public int? TurnoId { get; set; }
    public EstadoCobroCliente Estado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public int? AnuladoPorId { get; set; }
    public int CreadoPorId { get; set; }
    public DateTime FechaCreacion { get; set; }

    public bool Vigente => Estado == EstadoCobroCliente.Vigente;
    public bool EsDivisa => Venta.EsDivisa(Metodo);
    public bool EsEfectivo => Metodo is MetodoPago.EfectivoBs or MetodoPago.EfectivoDivisa;
    public string MetodoTexto => Venta.TextoDe(Metodo);
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string MontoTexto => MontoBs.ToString("N2");
    public string MontoDivisaTexto => EsDivisa ? MontoDivisa.ToString("N2") : "—";
    public string EstadoTexto => Vigente ? "Vigente" : "Anulado";

    public CobroCliente Clonar() => (CobroCliente)MemberwiseClone();
}

/// <summary>Movimiento inalterable del estado de cuenta; el signo lo determina su tipo.</summary>
public class MovimientoCuentaPorCobrar : IEntidad<int>, IDeOrganizacion
{
    public int OrganizacionId { get; set; }
    public int Id { get; set; }
    public int CuentaPorCobrarId { get; set; }
    public int? CobroClienteId { get; set; }
    public int? DocumentoVentaId { get; set; }
    public string DocumentoOrigen { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public TipoMovimientoCuentaPorCobrar Tipo { get; set; }
    public decimal MontoBs { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public int CreadoPorId { get; set; }
    public DateTime FechaCreacion { get; set; }

    public decimal EfectoBs => Tipo is TipoMovimientoCuentaPorCobrar.Cobro
        or TipoMovimientoCuentaPorCobrar.RetencionIva
        or TipoMovimientoCuentaPorCobrar.NotaCredito ? -MontoBs : MontoBs;
    public string TipoTexto => Tipo switch
    {
        TipoMovimientoCuentaPorCobrar.Apertura => "Apertura",
        TipoMovimientoCuentaPorCobrar.Cobro => "Cobro",
        TipoMovimientoCuentaPorCobrar.AnulacionCobro => "Anulación de cobro",
        TipoMovimientoCuentaPorCobrar.RetencionIva => "Retención de IVA",
        TipoMovimientoCuentaPorCobrar.NotaCredito => "Nota de crédito",
        _ => "Nota de débito"
    };
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string MontoTexto => EfectoBs.ToString("N2");
}
