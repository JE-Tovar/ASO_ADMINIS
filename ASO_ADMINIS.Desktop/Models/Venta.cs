namespace ASO_ADMINIS.Desktop.Models;

/// <summary>Estados de una venta. Se persiste como ORDINAL: miembros nuevos al final.</summary>
public enum EstadoVenta
{
    Registrada,
    Anulada
}

/// <summary>
/// Venta de un modelo del catálogo: registro mínimo (fecha, modelo, cantidad, precio, cliente
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

    public int ModeloId { get; set; }
    public string ModeloNombre { get; set; } = string.Empty;  // snapshot
    public string MarcaNombre { get; set; } = string.Empty;   // snapshot

    public decimal Cantidad { get; set; }

    /// <summary>Precio del modelo al momento de vender — un cambio de precio después no debe
    /// alterar ventas ya hechas.</summary>
    public decimal PrecioUnitarioUsd { get; set; }

    /// <summary>Tasa de cambio vigente al momento de vender, snapshot igual que el precio.</summary>
    public decimal TasaCambioUsada { get; set; }

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
    public string ModeloTexto => $"{MarcaNombre} · {ModeloNombre}";
    public string CantidadTexto => Cantidad.ToString("N2");
    public string PrecioUnitarioTexto => PrecioUnitarioUsd.ToString("N2");
    public string TotalUsdTexto => TotalUsd.ToString("N2");
    public string TotalBsTexto => TotalBs.ToString("N2");

    /// <summary>Copia superficial (solo hay tipos de valor y cadenas) para no mutar el original en la lista.</summary>
    public Venta Clonar() => (Venta)MemberwiseClone();
}
