using System;
using System.Collections.Generic;
using System.Linq;

namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// De dónde viene lo que entra al almacén. Se persiste como ORDINAL: miembros nuevos al final.
///
/// Las dos primeras generan cuenta por pagar en Finanzas; <see cref="Ajuste"/> no, porque no hay
/// nadie a quien deberle — es el asiento con el que se carga el inventario inicial o se corrige
/// un conteo.
/// </summary>
public enum TipoEntrada
{
    CompraProveedor,
    CompraExterna,
    Ajuste
}

/// <summary>Estados de una entrada. Se persiste como ORDINAL: miembros nuevos al final.</summary>
public enum EstadoEntrada
{
    Registrada,
    Anulada
}

/// <summary>
/// Documento de entrada al almacén: lo que llegó, de quién y a qué precio.
///
/// Es la raíz de un agregado (ver <see cref="Lineas"/>) y el origen de una cuenta por pagar:
/// registrarla crea la <see cref="FacturaProveedor"/> correspondiente en Finanzas, salvo cuando
/// es un <see cref="TipoEntrada.Ajuste"/>. El enlace al documento generado se guarda como un
/// <c>int</c> suelto sin clave foránea real, igual que <see cref="MovimientoBanco.OrigenId"/>.
/// </summary>
public class EntradaInventario : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoAdminisDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>Correlativo interno del almacén, "ENT-000123". Lo asigna
    /// <c>EntradasInventarioService</c> al registrar, no el formulario.</summary>
    public string Numero { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    public TipoEntrada Tipo { get; set; }

    /// <summary>
    /// A quién se le compró. Nulo solo en un <see cref="TipoEntrada.Ajuste"/>. En una compra
    /// externa apunta al comercio, dado de alta al vuelo en el padrón si no existía.
    /// </summary>
    public int? ProveedorId { get; set; }

    public string ProveedorNombre { get; set; } = string.Empty;  // snapshot

    /// <summary>Número de la factura o del recibo que trae el proveedor o el comercio; no es el
    /// correlativo interno. Es lo que evita cargar dos veces la misma compra.</summary>
    public string NumeroDocumento { get; set; } = string.Empty;

    /// <summary>Plazo de pago que hereda la cuenta por pagar.</summary>
    public DateTime? FechaVencimiento { get; set; }

    /// <summary>Quién de la empresa hizo la compra. Solo en
    /// <see cref="TipoEntrada.CompraExterna"/>.</summary>
    public string CompradoPor { get; set; } = string.Empty;

    public string Observaciones { get; set; } = string.Empty;

    /// <summary>Lo que entró, artículo por artículo.</summary>
    public List<EntradaInventarioLinea> Lineas { get; set; } = [];

    /// <summary>Suma de los subtotales. Se guarda porque es lo que se le debe al proveedor y no
    /// debe cambiar si alguien toca un precio después.</summary>
    public decimal Total { get; set; }

    public EstadoEntrada Estado { get; set; }

    /// <summary>Cuenta por pagar que generó esta entrada, si generó alguna. Enlace suelto, sin
    /// clave foránea real, como el resto de las relaciones entre documentos.</summary>
    public int? FacturaProveedorId { get; set; }

    public string FacturaProveedorNumero { get; set; } = string.Empty;  // snapshot

    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    public int CreadoPorId { get; set; }
    public string CreadoPorNombre { get; set; } = string.Empty;  // snapshot
    public DateTime FechaCreacion { get; set; }

    /// <summary>Un ajuste no le debe nada a nadie; los otros dos tipos sí.</summary>
    public bool GeneraCuentaPorPagar => Tipo != TipoEntrada.Ajuste;

    /// <summary>Si esta entrada suma a las existencias. Un documento anulado deja de contar,
    /// que es lo que hace que anular devuelva el stock sin tocar ninguna otra fila.</summary>
    public bool CuentaEnKardex => Estado == EstadoEntrada.Registrada;

    public string TipoTexto => Tipo switch
    {
        TipoEntrada.CompraProveedor => "Compra a proveedor",
        TipoEntrada.CompraExterna => "Compra externa",
        _ => "Ajuste"
    };

    public string EstadoTexto => Estado == EstadoEntrada.Registrada ? "Registrada" : "Anulada";

    public string OrigenTexto => Tipo switch
    {
        TipoEntrada.Ajuste => "Ajuste de inventario",
        TipoEntrada.CompraExterna when !string.IsNullOrWhiteSpace(CompradoPor) =>
            $"{ProveedorNombre} · compró {CompradoPor}",
        _ => ProveedorNombre
    };

    public string TotalTexto => Total.ToString("N2");

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");

    public string VencimientoTexto => FechaVencimiento is { } vencimiento
        ? vencimiento.ToString("dd/MM/yyyy")
        : "Sin definir";

    public string CuentaPorPagarTexto => string.IsNullOrWhiteSpace(FacturaProveedorNumero)
        ? "—"
        : $"Nº {FacturaProveedorNumero}";

    public int CantidadLineas => Lineas.Count;

    public decimal TotalUnidades => Lineas.Sum(l => l.Cantidad);

    /// <summary>
    /// Copia con las líneas duplicadas de verdad: <c>MemberwiseClone</c> compartiría la misma
    /// lista entre el original y la copia, y editar la copia mutaría lo que está en pantalla.
    /// </summary>
    public EntradaInventario Clonar()
    {
        var copia = (EntradaInventario)MemberwiseClone();
        copia.Lineas = Lineas.Select(l => l.Clonar()).ToList();
        return copia;
    }
}

/// <summary>
/// Renglón de una entrada. Guarda el <c>ArticuloId</c> para poder sumar el kardex, y además el
/// código, el nombre y la unidad como snapshot de texto, para que el documento siga leyéndose
/// igual aunque el artículo se renombre después.
/// </summary>
public class EntradaInventarioLinea
{
    public int ArticuloId { get; set; }
    public string ArticuloCodigo { get; set; } = string.Empty;  // snapshot
    public string ArticuloNombre { get; set; } = string.Empty;  // snapshot
    public string UnidadTexto { get; set; } = string.Empty;     // snapshot

    public decimal Cantidad { get; set; }

    /// <summary>Cero en un ajuste: ahí no se compró nada.</summary>
    public decimal PrecioUnitario { get; set; }

    public decimal Subtotal { get; set; }

    public string CantidadTexto => $"{Cantidad:N2} {UnidadTexto}".Trim();
    public string PrecioUnitarioTexto => PrecioUnitario.ToString("N2");
    public string SubtotalTexto => Subtotal.ToString("N2");
    public string ArticuloTexto => $"{ArticuloCodigo} · {ArticuloNombre}";

    public EntradaInventarioLinea Clonar() => (EntradaInventarioLinea)MemberwiseClone();
}
