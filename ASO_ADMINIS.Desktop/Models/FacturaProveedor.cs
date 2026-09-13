using System;
using System.Collections.Generic;
using System.Linq;

namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Estados de una factura de compra. La emite el proveedor, así que cuando llega a la
/// organización ya existe y solo queda pagarla o anularla. "Vencida" es condición derivada de
/// la fecha (ver <see cref="FacturaProveedor.EstaVencida"/>), no un estado guardado.
/// </summary>
public enum EstadoFacturaProveedor
{
    Pendiente,
    Pagada,
    Anulada
}

/// <summary>
/// Factura de un proveedor: lo que la organización debe por lo que le compró.
/// </summary>
public class FacturaProveedor : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoAdminisDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>Número que trae el documento del proveedor, no un correlativo interno.</summary>
    public string NumeroDocumento { get; set; } = string.Empty;

    public int ProveedorId { get; set; }
    public string ProveedorNombre { get; set; } = string.Empty;  // snapshot

    public string Descripcion { get; set; } = string.Empty;

    public DateTime FechaEmision { get; set; }

    public DateTime? FechaVencimiento { get; set; }

    public decimal Monto { get; set; }

    /// <summary>Detalle de lo que se compró y su precio — snapshot de texto.</summary>
    public List<FacturaProveedorLinea> Lineas { get; set; } = [];

    public EstadoFacturaProveedor Estado { get; set; }
    public DateTime? FechaPago { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    public int CreadoPorId { get; set; }
    public DateTime FechaCreacion { get; set; }

    /// <summary>Pendiente, con vencimiento definido y con el plazo cumplido. Es condición
    /// derivada, no un estado guardado.</summary>
    public bool EstaVencida =>
        Estado == EstadoFacturaProveedor.Pendiente
        && FechaVencimiento is { } vencimiento
        && vencimiento.Date < DateTime.Today;

    /// <summary>Días de atraso; negativo mientras falte para el vencimiento.</summary>
    public int DiasParaVencer => FechaVencimiento is { } vencimiento
        ? (vencimiento.Date - DateTime.Today).Days
        : 0;

    public string EstadoTexto => EstaVencida
        ? "Vencida"
        : Estado switch
        {
            EstadoFacturaProveedor.Pendiente => "Pendiente",
            EstadoFacturaProveedor.Pagada => "Pagada",
            _ => "Anulada"
        };

    public string MontoTexto => Monto.ToString("N2");

    public string VencimientoTexto => FechaVencimiento is { } vencimiento
        ? vencimiento.ToString("dd/MM/yyyy")
        : "Sin definir";

    public string PlazoTexto => Estado switch
    {
        EstadoFacturaProveedor.Pagada => FechaPago is { } pago ? $"Pagada el {pago:dd/MM/yyyy}" : "Pagada",
        EstadoFacturaProveedor.Anulada => "Anulada",
        _ when EstaVencida => $"Vencida hace {-DiasParaVencer} día(s)",
        _ => $"Vence en {DiasParaVencer} día(s)"
    };

    /// <summary>
    /// Copia con las líneas duplicadas de verdad: <c>MemberwiseClone</c> compartiría la misma
    /// lista entre el original y la copia, y editar la copia mutaría lo que está en pantalla.
    /// </summary>
    public FacturaProveedor Clonar()
    {
        var copia = (FacturaProveedor)MemberwiseClone();
        copia.Lineas = Lineas.Select(l => l.Clonar()).ToList();
        return copia;
    }
}

/// <summary>
/// Línea de una factura de proveedor: detalle de lo que se compró y su precio, snapshot de
/// texto (no referencia ningún catálogo).
/// </summary>
public class FacturaProveedorLinea
{
    public string DestinoTexto { get; set; } = string.Empty;
    public string CantidadTexto { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }

    public string PrecioUnitarioTexto => PrecioUnitario.ToString("N2");
    public string SubtotalTexto => Subtotal.ToString("N2");

    public FacturaProveedorLinea Clonar() => (FacturaProveedorLinea)MemberwiseClone();
}
