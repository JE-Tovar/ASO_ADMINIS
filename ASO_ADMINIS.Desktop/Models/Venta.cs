namespace ASO_ADMINIS.Desktop.Models;

/// <summary>Estados de un documento de venta. Se persiste como ORDINAL: miembros nuevos al final.</summary>
public enum EstadoVenta
{
    /// <summary>Emitido por el emisor fiscal: ya tiene número y control fiscales.</summary>
    Registrada,

    /// <summary>
    /// Solo historial: las ventas anteriores al documento fiscal se anulaban editando la fila.
    /// Un documento fiscal NO se anula: se corrige con una nota de crédito.
    /// </summary>
    Anulada,

    /// <summary>
    /// Emitido en papel de contingencia (el emisor no respondía). Ya es válido, pero falta
    /// transmitirlo al emisor fiscal — ver <c>VentasService.TransmitirPendientes</c>.
    /// </summary>
    PendienteTransmitir
}

/// <summary>Tipo de documento fiscal. Se persiste como ORDINAL: miembros nuevos al final.</summary>
public enum TipoDocumentoVenta
{
    Factura,
    NotaCredito,
    NotaDebito
}

/// <summary>Cómo se cobró. Se persiste como ORDINAL: miembros nuevos al final.</summary>
public enum MetodoPago
{
    EfectivoBs,
    EfectivoDivisa,
    Debito,
    Credito,
    PagoMovil,
    Transferencia,
    Zelle,
    Otro
}

/// <summary>
/// Documento fiscal de venta: factura, nota de crédito o nota de débito, con sus renglones y
/// (en la factura) sus pagos.
///
/// <b>Es inalterable una vez emitido.</b> No se edita ni se borra; un error se corrige con una
/// nota de crédito o de débito que referencia a la factura. <c>AsoAdminisDbContext.SaveChanges</c>
/// lo hace cumplir para cualquier escritura, venga de donde venga.
///
/// Los montos van en <b>bolívares</b>, convertidos con la tasa BCV del momento
/// (<see cref="TasaCambioUsada"/>); el precio en US$ de cada renglón queda como referencia. Los
/// totales por alícuota se calculan al emitir (<c>Services/CalculoFiscal.cs</c>) y se guardan
/// como snapshot, igual que los porcentajes: un cambio posterior de alícuota no altera
/// documentos emitidos.
///
/// Al emitirse una factura se genera un <see cref="SalidaInventario"/> vinculado para que el
/// kardex la cuente; una nota de crédito devuelve la mercancía con una
/// <see cref="EntradaInventario"/> de tipo <see cref="TipoEntrada.DevolucionVenta"/>.
/// </summary>
public class Venta : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoAdminisDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>Correlativo interno, "VTA-000123". Lo asigna <see cref="Services.VentasService"/>.</summary>
    public string Numero { get; set; } = string.Empty;

    public TipoDocumentoVenta Tipo { get; set; } = TipoDocumentoVenta.Factura;

    /// <summary>Fecha y hora de emisión.</summary>
    public DateTime Fecha { get; set; }

    /// <summary>Caja o terminal que emitió (de los ajustes de la máquina).</summary>
    public string CajaCodigo { get; set; } = string.Empty;

    // ---- Documento afectado (solo notas de crédito y débito) ----

    public int? DocumentoAfectadoId { get; set; }
    public string DocumentoAfectadoNumero { get; set; } = string.Empty;  // n° fiscal, snapshot
    public DateTime? DocumentoAfectadoFecha { get; set; }

    /// <summary>Motivo de una nota ("Devolución por defecto de fábrica"). Vacío en una factura.</summary>
    public string Concepto { get; set; } = string.Empty;

    // ---- Cliente (snapshot) ----

    /// <summary>Nulo = consumidor final.</summary>
    public int? ClienteId { get; set; }
    public string ClienteRif { get; set; } = string.Empty;
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteDireccion { get; set; } = string.Empty;

    /// <summary>Bs por US$ al momento de emitir, snapshot.</summary>
    public decimal TasaCambioUsada { get; set; }

    public List<VentaLinea> Lineas { get; set; } = [];
    public List<VentaPago> Pagos { get; set; } = [];

    // ---- Totales en Bs (snapshot, los calcula CalculoFiscal) ----

    public decimal MontoExento { get; set; }

    public decimal BaseGeneral { get; set; }
    public decimal PorcentajeGeneral { get; set; }
    public decimal IvaGeneral { get; set; }

    public decimal BaseReducida { get; set; }
    public decimal PorcentajeReducida { get; set; }
    public decimal IvaReducida { get; set; }

    public decimal BaseAdicional { get; set; }
    public decimal PorcentajeAdicional { get; set; }
    public decimal IvaAdicional { get; set; }

    /// <summary>Lo cobrado en divisas (en Bs) sobre lo que se percibe el IGTF.</summary>
    public decimal BaseIgtf { get; set; }
    public decimal PorcentajeIgtf { get; set; }
    public decimal Igtf { get; set; }

    /// <summary>Total a pagar: bases + exento + IVA + IGTF.</summary>
    public decimal Total { get; set; }

    /// <summary>Lo que se devolvió al cliente porque pagó de más.</summary>
    public decimal Vuelto { get; set; }

    // ---- Datos del emisor fiscal ----

    /// <summary>Número del documento según el emisor (máquina fiscal o imprenta digital).</summary>
    public string NumeroFiscal { get; set; } = string.Empty;

    /// <summary>Número de control asignado por el emisor (o el del formato de contingencia).</summary>
    public string NumeroControl { get; set; } = string.Empty;

    /// <summary>Serial de la máquina fiscal o identificador del proveedor de imprenta digital.</summary>
    public string SerialEmisor { get; set; } = string.Empty;

    /// <summary>Reporte Z que lo incluyó (solo con máquina fiscal); lo estampa el cierre.</summary>
    public int? NumeroZ { get; set; }

    /// <summary>Registrado en papel de contingencia.</summary>
    public bool EnContingencia { get; set; }

    /// <summary>Cuándo se transmitió al emisor un documento de contingencia.</summary>
    public DateTime? FechaTransmision { get; set; }

    /// <summary>
    /// Venta anterior al documento fiscal (cuando Ventas era de una sola línea, sin IVA ni emisor).
    /// Se conserva como historial y no entra al libro de ventas.
    /// </summary>
    public bool EsHistorico { get; set; }

    public EstadoVenta Estado { get; set; }

    /// <summary>Solo historial (ver <see cref="EstadoVenta.Anulada"/>).</summary>
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    public int CreadoPorId { get; set; }
    public DateTime FechaCreacion { get; set; }

    // ---- Derivados ----

    /// <summary>Si el documento cuenta en ventas y en el libro: emitido (o en contingencia) y no histórico anulado.</summary>
    public bool Vigente => Estado != EstadoVenta.Anulada;

    /// <summary>+1 o −1: una nota de crédito resta de las ventas.</summary>
    public int Signo => Tipo == TipoDocumentoVenta.NotaCredito ? -1 : 1;

    public decimal BaseImponible => BaseGeneral + BaseReducida + BaseAdicional;
    public decimal TotalIva => IvaGeneral + IvaReducida + IvaAdicional;

    /// <summary>Total sin IGTF: lo que el libro llama "total ventas incluyendo IVA".</summary>
    public decimal TotalConIva => Total - Igtf;

    /// <summary>Referencia en US$ a la tasa usada.</summary>
    public decimal TotalUsd => TasaCambioUsada > 0 ? Math.Round(Total / TasaCambioUsada, 2) : 0m;

    public bool EsConsumidorFinal => string.IsNullOrWhiteSpace(ClienteRif);

    public string TipoTexto => TextoDe(Tipo);

    public static string TextoDe(TipoDocumentoVenta tipo) => tipo switch
    {
        TipoDocumentoVenta.NotaCredito => "Nota de crédito",
        TipoDocumentoVenta.NotaDebito => "Nota de débito",
        _ => "Factura"
    };

    public string TipoCorto => Tipo switch
    {
        TipoDocumentoVenta.NotaCredito => "NC",
        TipoDocumentoVenta.NotaDebito => "ND",
        _ => "FAC"
    };

    public string EstadoTexto => Estado switch
    {
        EstadoVenta.Anulada => "Anulada",
        EstadoVenta.PendienteTransmitir => "Por transmitir",
        _ => EsHistorico ? "Histórico" : "Emitida"
    };

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string FechaHoraTexto => Fecha.ToString("dd/MM/yyyy HH:mm");
    public string ClienteTexto => EsConsumidorFinal ? (string.IsNullOrWhiteSpace(ClienteNombre) ? "Consumidor final" : ClienteNombre) : $"{ClienteRif} · {ClienteNombre}";

    public string ArticuloTexto => Lineas.Count switch
    {
        0 => Concepto,
        1 => Lineas[0].Descripcion,
        _ => $"{Lineas[0].Descripcion} y {Lineas.Count - 1} más"
    };

    public string MetodoPagoTexto => Pagos.Count switch
    {
        0 => Tipo == TipoDocumentoVenta.Factura ? "No indicado" : "—",
        1 => TextoDe(Pagos[0].Metodo),
        _ => "Mixto: " + string.Join(" + ", Pagos.Select(p => TextoDe(p.Metodo)))
    };

    public string NumeroFiscalTexto => string.IsNullOrWhiteSpace(NumeroFiscal) ? "—" : NumeroFiscal;
    public string TotalTexto => Total.ToString("N2");
    public string TotalBsTexto => Total.ToString("N2");
    public string TotalUsdTexto => TotalUsd.ToString("N2");

    /// <summary>Nombre para mostrar de cada método; lo comparten la grilla, el editor y el reporte.</summary>
    public static string TextoDe(MetodoPago metodo) => metodo switch
    {
        MetodoPago.EfectivoBs => "Efectivo Bs",
        MetodoPago.EfectivoDivisa => "Efectivo divisa",
        MetodoPago.Debito => "Débito",
        MetodoPago.Credito => "Crédito",
        MetodoPago.PagoMovil => "Pago móvil",
        MetodoPago.Transferencia => "Transferencia",
        MetodoPago.Zelle => "Zelle",
        _ => "Otro"
    };

    /// <summary>Si el método es en moneda extranjera: lo que causa IGTF.</summary>
    public static bool EsDivisa(MetodoPago metodo) =>
        metodo is MetodoPago.EfectivoDivisa or MetodoPago.Zelle;

    /// <summary>Copia que también duplica renglones y pagos, para no mutar lo que está en pantalla.</summary>
    public Venta Clonar()
    {
        var copia = (Venta)MemberwiseClone();
        copia.Lineas = Lineas.Select(l => l.Clonar()).ToList();
        copia.Pagos = Pagos.Select(p => p.Clonar()).ToList();
        return copia;
    }
}

/// <summary>
/// Un renglón del documento. En una factura o nota de crédito es un artículo del almacén o un
/// servicio (<see cref="EsServicio"/>); en
/// una nota de débito puede ser un concepto libre (<see cref="ArticuloId"/> nulo).
/// </summary>
public class VentaLinea
{
    public int? ArticuloId { get; set; }

    /// <summary>
    /// Snapshot: el renglón es un servicio (no mueve el almacén ni al facturar ni al devolver).
    /// Se guarda en el renglón y no se consulta al artículo porque la factura es inalterable.
    /// </summary>
    public bool EsServicio { get; set; }

    public string Codigo { get; set; } = string.Empty;       // snapshot (SKU)
    public string Descripcion { get; set; } = string.Empty;  // snapshot, sin genéricos

    /// <summary>Presentación vendida (snapshot): "Galón", o la unidad base ("L").</summary>
    public string PresentacionNombre { get; set; } = string.Empty;

    /// <summary>Cuánto de la unidad base trae la presentación. 1 = unidad base.</summary>
    public decimal Factor { get; set; } = 1m;

    /// <summary>En la presentación vendida; lo que sale del almacén es Cantidad × Factor.</summary>
    public decimal Cantidad { get; set; }

    /// <summary>Precio de referencia en US$, sin IVA.</summary>
    public decimal PrecioUnitarioUsd { get; set; }

    /// <summary>Precio unitario en Bs sin IVA: el que figura en el documento.</summary>
    public decimal PrecioUnitario { get; set; }

    public TipoAlicuota Alicuota { get; set; }
    public decimal PorcentajeIva { get; set; }

    /// <summary>Cantidad × precio unitario (Bs, sin IVA).</summary>
    public decimal Monto { get; set; }

    public decimal Iva { get; set; }

    public decimal CantidadBase => Cantidad * Factor;
    /// <summary>"1,25 kg", "2 pza": hasta 3 decimales, sin ceros de relleno.</summary>
    public string CantidadTexto => $"{Cantidad:#,0.###} {PresentacionNombre}".Trim();
    public string PrecioUnitarioTexto => PrecioUnitario.ToString("N2");
    public string MontoTexto => Monto.ToString("N2");
    public string AlicuotaTexto => Alicuota == TipoAlicuota.Exento ? "(E)" : $"{PorcentajeIva:0.##} %";

    public VentaLinea Clonar() => (VentaLinea)MemberwiseClone();
}

/// <summary>Un medio de pago de la factura (puede haber varios: pago mixto).</summary>
public class VentaPago
{
    public MetodoPago Metodo { get; set; }

    /// <summary>Monto en Bs (para un pago en divisa, su equivalente a la tasa del documento).</summary>
    public decimal MontoBs { get; set; }

    /// <summary>Monto en la divisa, si el método es en divisa; 0 si no.</summary>
    public decimal MontoDivisa { get; set; }

    /// <summary>Referencia bancaria, últimos dígitos de la tarjeta…</summary>
    public string Referencia { get; set; } = string.Empty;

    public bool EsDivisa => Venta.EsDivisa(Metodo);
    public string MetodoTexto => Venta.TextoDe(Metodo);

    public VentaPago Clonar() => (VentaPago)MemberwiseClone();
}
