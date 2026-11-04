namespace ASO_ADMINIS.Desktop.Models;

/// <summary>Estados de una venta. Se persiste como ORDINAL: miembros nuevos al final.</summary>
public enum EstadoVenta
{
    Registrada,
    Anulada
}

/// <summary>Cómo se cobró una venta. Se persiste como ORDINAL: miembros nuevos al final.</summary>
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
/// Venta de un artículo del almacén: registro mínimo (fecha, artículo, cantidad, precio, cliente
/// opcional), primer recorte real de un futuro módulo de Ventas completo.
///
/// Una sola línea a propósito — no es un carrito multi-artículo. Al registrarse genera un
/// <see cref="SalidaInventario"/> vinculado (ver <see cref="Services.VentasService"/>) para que
/// el kardex la cuente sin que Inventario tenga que saber nada de Ventas.
/// </summary>
public class Venta : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoAdminisDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>Correlativo, "VTA-000123". Lo asigna <see cref="Services.VentasService"/> al registrar.</summary>
    public string Numero { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    public int ArticuloId { get; set; }
    public string ArticuloCodigo { get; set; } = string.Empty;  // snapshot
    public string ArticuloNombre { get; set; } = string.Empty;  // snapshot

    /// <summary>
    /// En la presentación vendida ("2" galones), no en la unidad base: es lo que figura en la
    /// venta. Lo que se descuenta del almacén es <c>Cantidad × Factor</c>.
    /// </summary>
    public decimal Cantidad { get; set; }

    /// <summary>Presentación vendida (snapshot): "Galón", o la unidad base ("L").</summary>
    public string PresentacionNombre { get; set; } = string.Empty;

    /// <summary>Cuánto de la unidad base trae la presentación vendida. 1 = unidad base.</summary>
    public decimal Factor { get; set; } = 1m;

    /// <summary>Lo que sale del almacén, en unidad base.</summary>
    public decimal CantidadBase => Cantidad * Factor;

    /// <summary>Precio por presentación vendida, del artículo al momento de vender — un cambio de precio después no debe
    /// alterar ventas ya hechas.</summary>
    public decimal PrecioUnitarioUsd { get; set; }

    /// <summary>Tasa de cambio vigente al momento de vender, snapshot igual que el precio.</summary>
    public decimal TasaCambioUsada { get; set; }

    /// <summary>
    /// Cómo se cobró. Nulo solo en las ventas registradas antes de que existiera el campo (se
    /// muestran como "No indicado" en vez de inventarles un método); en una venta nueva lo
    /// exige <see cref="Services.VentasService.Validar"/>.
    /// </summary>
    public MetodoPago? MetodoPago { get; set; }

    /// <summary>Texto libre; no todos los clientes son un padrón.</summary>
    public string ClienteNombre { get; set; } = string.Empty;

    public EstadoVenta Estado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    public int CreadoPorId { get; set; }
    public DateTime FechaCreacion { get; set; }

    /// <summary>Si esta venta resta de las existencias — vía su Salida vinculada, no directamente.</summary>
    public bool CuentaEnKardex => Estado == EstadoVenta.Registrada;

    public decimal TotalUsd => Cantidad * PrecioUnitarioUsd;
    public decimal TotalBs => TotalUsd * TasaCambioUsada;

    public string EstadoTexto => Estado == EstadoVenta.Registrada ? "Registrada" : "Anulada";
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string ArticuloTexto => $"{ArticuloCodigo} · {ArticuloNombre}";
    public string CantidadTexto => $"{Cantidad:N2} {PresentacionNombre}".Trim();

    /// <summary>Solo el número, para la grilla, donde la presentación va en su propia columna.</summary>
    public string CantidadNumeroTexto => Cantidad.ToString("N2");
    public string PrecioUnitarioTexto => PrecioUnitarioUsd.ToString("N2");
    public string TotalUsdTexto => TotalUsd.ToString("N2");
    public string TotalBsTexto => TotalBs.ToString("N2");
    public string MetodoPagoTexto => MetodoPago is { } metodo ? TextoDe(metodo) : "No indicado";

    /// <summary>Nombre para mostrar de cada método; lo comparten la grilla, el editor y el reporte.</summary>
    public static string TextoDe(MetodoPago metodo) => metodo switch
    {
        Models.MetodoPago.EfectivoBs => "Efectivo Bs",
        Models.MetodoPago.EfectivoDivisa => "Efectivo divisa",
        Models.MetodoPago.Debito => "Débito",
        Models.MetodoPago.Credito => "Crédito",
        Models.MetodoPago.PagoMovil => "Pago móvil",
        Models.MetodoPago.Transferencia => "Transferencia",
        Models.MetodoPago.Zelle => "Zelle",
        _ => "Otro"
    };

    /// <summary>Copia superficial (solo hay tipos de valor y cadenas) para no mutar el original en la lista.</summary>
    public Venta Clonar() => (Venta)MemberwiseClone();
}
