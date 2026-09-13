namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Categoría de producto que vende la organización. Se persiste como ORDINAL, así que los
/// miembros nuevos se añaden SIEMPRE al final.
/// </summary>
public enum CategoriaProducto
{
    Calzado,
    Ropa,
    CorreasYCarteras,
    Otro
}

/// <summary>
/// Modelo que la organización vende de una <see cref="Marca"/> (p. ej. "Oxford Negro" de
/// "Bata"). Dato maestro de catálogo: precio y clasificación del producto.
///
/// Al darse de alta genera (y mientras viva mantiene sincronizado) el <see cref="Articulo"/> de
/// Inventario con el que se lleva su existencia — ver <see cref="Articulo.ModeloId"/> y
/// <see cref="Services.CatalogoService"/>. Este modelo no conoce ese vínculo: la orquesta
/// <c>CatalogoService</c>, que es quien conoce las dos tablas.
/// </summary>
public class Modelo : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoAdminisDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    public int MarcaId { get; set; }

    /// <summary>Snapshot de texto de la marca, mismo criterio que el resto del scaffold
    /// (p. ej. <c>FacturaProveedor.ProveedorNombre</c>): no hay clave foránea real.</summary>
    public string MarcaNombre { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public CategoriaProducto Categoria { get; set; }

    public UnidadMedida Unidad { get; set; } = UnidadMedida.Par;

    /// <summary>Precio de venta de referencia. Todavía no hay módulo de Ventas que lo use.</summary>
    public decimal PrecioVenta { get; set; }

    public string Notas { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public string EstadoTexto => Activo ? "Activo" : "Inactivo";

    public string CategoriaTexto => Categoria switch
    {
        CategoriaProducto.Calzado => "Calzado",
        CategoriaProducto.Ropa => "Ropa",
        CategoriaProducto.CorreasYCarteras => "Correas y carteras",
        _ => "Otro"
    };

    public string PrecioVentaTexto => PrecioVenta.ToString("N2");

    /// <summary>
    /// Equivalente en bolívares a la tasa vigente. NO se persiste (va con <c>Ignore</c> en el
    /// DbContext): depende de una tabla aparte y este modelo no tiene acceso a la base. La
    /// rellena <c>ModelosViewModel</c> con <see cref="Services.TasaCambioService"/> antes de
    /// mostrar la lista, igual que <see cref="Articulo.Existencia"/>.
    /// </summary>
    public decimal PrecioVentaBs { get; set; }

    public string PrecioVentaBsTexto => PrecioVentaBs > 0 ? PrecioVentaBs.ToString("N2") : "—";

    public string Etiqueta => $"{MarcaNombre} · {Nombre}";

    /// <summary>Copia superficial (solo hay tipos de valor y cadenas) para no mutar el original en la lista.</summary>
    public Modelo Clonar() => (Modelo)MemberwiseClone();
}
