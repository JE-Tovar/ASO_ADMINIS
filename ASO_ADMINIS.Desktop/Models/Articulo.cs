namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Unidad en que se cuenta un artículo. Se persiste como ORDINAL, así que los miembros nuevos
/// se añaden SIEMPRE al final.
/// </summary>
public enum UnidadMedida
{
    Pieza,
    Caja,
    Paleta,
    Kilogramo,
    Litro,
    Metro,
    Rollo,
    Saco,
    Par
}

/// <summary>
/// Artículo del almacén: la mercancía que la organización compra, guarda y vende, sea cual sea
/// el rubro (un par de zapatos, un galón de pintura, una caja de tornillos). Dato maestro de la
/// organización y, a la vez, lo que se elige al registrar una venta (ver <see cref="Venta"/>).
///
/// No guarda su existencia. La existencia se DERIVA del kardex (lo que entró menos lo que
/// salió, sin contar documentos anulados) porque un número editable a mano se desincroniza del
/// historial y no deja rastro de por qué cambió. Ver <see cref="Existencia"/>.
/// </summary>
public class Articulo : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoAdminisDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>
    /// Código con el que la organización identifica el artículo. Único dentro de la organización. Si
    /// quien lo da de alta no escribe uno, <c>InventarioService.GenerarCodigo</c> le pone uno
    /// aleatorio y legible.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Agrupación libre (Calzado, Pinturas, Ferretería…); no es un catálogo aparte
    /// mientras no haya una necesidad real de mantenerlo.</summary>
    public string Categoria { get; set; } = string.Empty;

    public UnidadMedida Unidad { get; set; }

    /// <summary>Existencia por debajo de la cual el artículo se marca en el almacén. Cero
    /// significa "no vigilar".</summary>
    public decimal Minimo { get; set; }

    /// <summary>Dónde está físicamente: pasillo, estante, zona.</summary>
    public string Ubicacion { get; set; } = string.Empty;

    public string Notas { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    /// <summary>Precio de venta de referencia, en dólares. Ventas lo propone al elegir el
    /// artículo y guarda su propio snapshot, así que cambiarlo no altera ventas ya hechas.</summary>
    public decimal PrecioVenta { get; set; }

    /// <summary>
    /// Equivalente en bolívares a la tasa vigente. NO se persiste (va con <c>Ignore</c> en el
    /// DbContext): depende de otra tabla y el modelo no tiene acceso a la base. La rellena
    /// <c>AlmacenViewModel</c> con <see cref="Services.TasaCambioService"/> antes de pintar,
    /// igual que <see cref="Existencia"/>.
    /// </summary>
    public decimal PrecioVentaBs { get; set; }

    /// <summary>
    /// Otras medidas en que se compra o se vende el artículo (galón, cuñete, caja de 12…), cada
    /// una con cuánto contiene de <see cref="Unidad"/>. La existencia se sigue contando SIEMPRE
    /// en la unidad base: los documentos guardan la presentación elegida y su factor como
    /// snapshot y convierten al registrar. Vacía si el artículo solo se maneja en su unidad.
    ///
    /// Es un dato por artículo y no un miembro más de <see cref="UnidadMedida"/> a propósito:
    /// así cada rubro tiene sus medidas sin alargar la lista de unidades de todos.
    /// </summary>
    public List<PresentacionArticulo> Presentaciones { get; set; } = [];

    /// <summary>
    /// Lo que se ofrece al elegir cómo se compra o se vende: primero la unidad base (factor 1,
    /// precio <see cref="PrecioVenta"/>), que nunca hace falta dar de alta, y luego las
    /// presentaciones. El precio de una presentación sin precio propio es factor × precio base.
    /// </summary>
    public IReadOnlyList<OpcionPresentacion> OpcionesDePresentacion() =>
    [
        new OpcionPresentacion(UnidadCorta, 1m, PrecioVenta, $"{UnidadTexto} (unidad base)"),
        .. Presentaciones.Select(p => new OpcionPresentacion(
            p.Nombre, p.Factor,
            p.PrecioVenta > 0 ? p.PrecioVenta : Math.Round(p.Factor * PrecioVenta, 2),
            $"{p.Nombre} ({p.Factor:0.####} {UnidadCorta})"))
    ];

    /// <summary>"Galón · Cuñete" para la grilla del almacén.</summary>
    public string PresentacionesTexto => Presentaciones.Count == 0
        ? "—"
        : string.Join(" · ", Presentaciones.Select(p => p.Nombre));

    /// <summary>
    /// NO se persiste (va con <c>Ignore</c> en el DbContext): depende de dos tablas enteras y el
    /// modelo no tiene acceso a la base. La rellena <c>InventarioService.RellenarExistencias</c>
    /// antes de mostrar la lista, igual que <see cref="CuentaBancaria.SaldoActual"/>.
    /// </summary>
    public decimal Existencia { get; set; }

    public bool BajoMinimo => Activo && Minimo > 0 && Existencia < Minimo;

    public bool SinExistencia => Activo && Existencia <= 0;

    public string EstadoTexto => !Activo
        ? "Inactivo"
        : SinExistencia
            ? "Sin existencia"
            : BajoMinimo
                ? "Bajo mínimo"
                : "Disponible";

    public string UnidadTexto => Unidad switch
    {
        UnidadMedida.Pieza => "Pieza",
        UnidadMedida.Caja => "Caja",
        UnidadMedida.Paleta => "Paleta",
        UnidadMedida.Kilogramo => "Kilogramo",
        UnidadMedida.Litro => "Litro",
        UnidadMedida.Metro => "Metro",
        UnidadMedida.Rollo => "Rollo",
        UnidadMedida.Saco => "Saco",
        _ => "Par"
    };

    /// <summary>Abreviatura para las grillas de líneas, donde no cabe el nombre completo.</summary>
    public string UnidadCorta => Unidad switch
    {
        UnidadMedida.Pieza => "pza",
        UnidadMedida.Caja => "caja",
        UnidadMedida.Paleta => "pal",
        UnidadMedida.Kilogramo => "kg",
        UnidadMedida.Litro => "L",
        UnidadMedida.Metro => "m",
        UnidadMedida.Rollo => "rollo",
        UnidadMedida.Saco => "saco",
        _ => "par"
    };

    public string ExistenciaTexto => Existencia.ToString("N2");

    public string MinimoTexto => Minimo > 0 ? Minimo.ToString("N2") : "—";

    public string PrecioVentaTexto => PrecioVenta.ToString("N2");

    public string PrecioVentaBsTexto => PrecioVentaBs > 0 ? PrecioVentaBs.ToString("N2") : "—";

    public string Etiqueta => $"{Codigo} · {Nombre}";

    /// <summary>
    /// Copia que también duplica las presentaciones: con un <c>MemberwiseClone</c> a secas la
    /// lista sería la misma en el original y en la copia, y editar la copia mutaría lo que está
    /// en pantalla — mismo criterio que <see cref="EntradaInventario.Clonar"/>.
    /// </summary>
    public Articulo Clonar()
    {
        var copia = (Articulo)MemberwiseClone();
        copia.Presentaciones = Presentaciones.Select(p => p.Clonar()).ToList();
        return copia;
    }
}

/// <summary>
/// Una medida en que se compra o se vende un <see cref="Articulo"/>: "Galón" = 3,785 litros.
/// Hija del artículo (se guarda con él, sin identidad propia de dominio), igual que las líneas
/// de un documento.
/// </summary>
public class PresentacionArticulo
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Cuánto de la unidad base del artículo contiene. Siempre mayor que cero.</summary>
    public decimal Factor { get; set; }

    /// <summary>Precio de venta propio, en US$. Cero = factor × precio base del artículo.</summary>
    public decimal PrecioVenta { get; set; }

    public PresentacionArticulo Clonar() => (PresentacionArticulo)MemberwiseClone();
}

/// <summary>
/// Una opción del combo "presentación" de Ventas y Entradas. <see cref="Nombre"/> es lo que se
/// guarda como snapshot en el documento; <see cref="Texto"/> es lo que se lee en el combo.
/// </summary>
public sealed record OpcionPresentacion(string Nombre, decimal Factor, decimal PrecioVenta, string Texto);
