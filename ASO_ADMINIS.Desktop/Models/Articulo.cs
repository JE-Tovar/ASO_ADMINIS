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
/// Qué es un <see cref="Articulo"/>. Se persiste como ORDINAL: miembros nuevos al final.
/// </summary>
public enum TipoArticulo
{
    /// <summary>Mercancía del almacén: tiene existencia y pasa por Entradas y Salidas.</summary>
    Bien,

    /// <summary>
    /// Prestación que se factura sin mover el almacén (cambio de aceite, recarga de batería). Se
    /// da de alta en Ventas · Servicios, no tiene existencia y no aparece en Inventario.
    /// </summary>
    Servicio
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

    /// <summary>
    /// Descripción clara y detallada del artículo ("Pintura caucho blanco satinado 1 gal"). Es lo
    /// que se imprime en cada renglón de la factura, así que no admite descripciones genéricas
    /// (ver <c>InventarioService.DescripcionGenerica</c>). El artículo se trabaja por
    /// <see cref="Codigo"/> primero y esta descripción después.
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

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

    /// <summary>
    /// Bien o servicio. Comparten tabla a propósito: el renglón de venta apunta igual a los dos
    /// (<c>VentaLinea.ArticuloId</c>), las notas de crédito y los reportes por categoría no
    /// distinguen, y el código es único entre ambos.
    /// </summary>
    public TipoArticulo Tipo { get; set; } = TipoArticulo.Bien;

    public bool EsServicio => Tipo == TipoArticulo.Servicio;

    /// <summary>
    /// Se vende a granel: por peso o medida, con decimales (1,250 kg, 3,5 m). Se deduce de la
    /// unidad base; lo demás (pieza, caja, saco…) y los servicios se venden en cantidades enteras.
    /// </summary>
    public bool EsGranel => !EsServicio && Unidad is UnidadMedida.Kilogramo or UnidadMedida.Litro or UnidadMedida.Metro;

    /// <summary>
    /// Código del producto en la balanza (PLU), solo para granel. Es lo que trae la etiqueta EAN-13
    /// que imprime la balanza (ver <c>Services/EtiquetaBalanza</c>). Vacío = no se pesa en balanza.
    /// </summary>
    public string CodigoBalanza { get; set; } = string.Empty;

    /// <summary>Precio de venta en dólares. Es EL precio: Ventas lo toma de aquí y quien
    /// factura no puede cambiarlo (lo hace cumplir <c>VentasService</c>). La factura guarda su
    /// propio snapshot, así que cambiarlo no altera ventas ya hechas.</summary>
    public decimal PrecioVenta { get; set; }

    /// <summary>
    /// Alícuota de IVA del artículo. <see cref="PrecioVenta"/> es SIN IVA: el impuesto se suma
    /// al facturar, con el porcentaje vigente de la organización.
    /// </summary>
    public TipoAlicuota Alicuota { get; set; } = TipoAlicuota.General;

    public string AlicuotaTexto => Alicuotas.Texto(Alicuota);

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
    /// Un servicio tiene una sola opción ("serv", factor 1): no se mide ni se presenta.
    /// </summary>
    public IReadOnlyList<OpcionPresentacion> OpcionesDePresentacion()
    {
        if (EsServicio)
            return [new OpcionPresentacion(UnidadCorta, 1m, PrecioVenta, "Servicio")];

        return
        [
            new OpcionPresentacion(UnidadCorta, 1m, PrecioVenta, $"{UnidadTexto} (unidad base)"),
            .. Presentaciones.Select(p => new OpcionPresentacion(
                p.Nombre, p.Factor,
                p.PrecioVenta > 0 ? p.PrecioVenta : Math.Round(p.Factor * PrecioVenta, 2),
                $"{p.Nombre} ({p.Factor:0.####} {UnidadCorta})"))
        ];
    }

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

    public bool BajoMinimo => !EsServicio && Activo && Minimo > 0 && Existencia < Minimo;

    public bool SinExistencia => !EsServicio && Activo && Existencia <= 0;

    public string EstadoTexto => !Activo
        ? "Inactivo"
        : EsServicio
            ? "Activo"
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
    public string UnidadCorta => EsServicio ? "serv" : Unidad switch
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

    /// <summary>"20,00 L", o "Servicio": lo que se lee en la lista del buscador de artículos.</summary>
    public string DisponibleTexto => EsServicio ? "Servicio" : $"{Existencia:N2} {UnidadCorta}";

    /// <summary>"Sin categoría" en vez de vacío, para la lista del buscador.</summary>
    public string CategoriaTexto => string.IsNullOrWhiteSpace(Categoria) ? "Sin categoría" : Categoria;

    public string MinimoTexto => Minimo > 0 ? Minimo.ToString("N2") : "—";

    public string PrecioVentaTexto => PrecioVenta.ToString("N2");

    public string PrecioVentaBsTexto => PrecioVentaBs > 0 ? PrecioVentaBs.ToString("N2") : "—";

    public string Etiqueta => $"{Codigo} · {Descripcion}";

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
