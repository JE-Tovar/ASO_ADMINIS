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
/// Artículo del almacén: tapas, etiquetas, detergente, cajas, insumos de la línea.
/// Dato maestro de la organización.
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
    /// Código con el que la planta identifica el artículo. Único dentro de la organización. Si
    /// quien lo da de alta no escribe uno, <c>InventarioService.GenerarCodigo</c> le pone uno
    /// aleatorio y legible.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Agrupación libre (Envases, Etiquetas, Químicos…); no es un catálogo aparte
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
    /// Modelo de Catálogo del que este artículo es el vínculo con Inventario. Nulo en los
    /// artículos que no vienen de un Modelo (compatibilidad con el scaffold genérico). Lo
    /// asigna <see cref="Services.CatalogoService"/> al crear el artículo; Nombre/Categoría/
    /// Activo quedan sincronizados desde el Modelo — Código, Unidad, Mínimo y Ubicación siguen
    /// siendo terreno exclusivo de esta pantalla.
    /// </summary>
    public int? ModeloId { get; set; }

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

    public string Etiqueta => $"{Codigo} · {Nombre}";

    /// <summary>Copia superficial (solo hay tipos de valor y cadenas) para no mutar el original
    /// en la lista.</summary>
    public Articulo Clonar() => (Articulo)MemberwiseClone();
}
