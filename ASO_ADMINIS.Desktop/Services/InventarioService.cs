using System;
using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Reglas del catálogo de artículos y del kardex.
///
/// El almacén no guarda existencias: las calcula. Este servicio es el único sitio donde vive esa
/// cuenta, y lo usan las tres pantallas del módulo — Almacén para pintarla, Salidas para no
/// dejar sacar más de lo que hay, y Entradas para no dejar anular lo que ya se despachó.
/// </summary>
public sealed class InventarioService
{
    /// <summary>
    /// Alfabeto del código generado, sin los caracteres que se confunden al dictar o al leer un
    /// papel: no lleva 0/O ni 1/I/L.
    /// </summary>
    private const string Alfabeto = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    private const string Prefijo = "ART-";

    private readonly IArticuloDataSource _articulos;
    private readonly IEntradaInventarioDataSource _entradas;
    private readonly ISalidaInventarioDataSource _salidas;
    private readonly Func<int> _digitosPlu;

    public InventarioService(IArticuloDataSource articulos,
                             IEntradaInventarioDataSource entradas,
                             ISalidaInventarioDataSource salidas,
                             Func<int>? digitosPlu = null)
    {
        _articulos = articulos;
        _entradas = entradas;
        _salidas = salidas;
        _digitosPlu = digitosPlu ?? (() => Ajustes.Actual.EtiquetaDigitosProducto);
    }

    // --- Código ---

    /// <summary>
    /// Código aleatorio y legible, tipo "ART-K7P2Q", para cuando quien da de alta el artículo no
    /// escribe uno propio.
    ///
    /// Comprueba contra los que ya existen, pero la garantía de verdad es el índice único
    /// <c>(OrganizacionId, Codigo)</c>: si dos puestos generasen el mismo candidato a la vez, el
    /// segundo choca contra la base en vez de duplicar el artículo.
    /// </summary>
    public string GenerarCodigo()
    {
        var usados = _articulos.GetAll()
            .Select(a => a.Codigo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var intento = 0; intento < 20; intento++)
        {
            var candidato = Prefijo + new string(Enumerable.Range(0, 5)
                .Select(_ => Alfabeto[Random.Shared.Next(Alfabeto.Length)])
                .ToArray());

            if (!usados.Contains(candidato))
                return candidato;
        }

        throw new InvalidOperationException(
            "No se pudo generar un código libre para el artículo; escriba uno a mano.");
    }

    // --- Validación del maestro ---

    private static readonly string[] Genericas =
    [
        "articulo", "articulos", "mercancia", "mercancias", "varios", "varias", "producto",
        "productos", "item", "items", "otros", "otro", "general", "miscelaneo", "miscelaneos",
        "servicio", "servicios", "mano de obra", "servicio tecnico", "trabajo", "trabajos"
    ];

    /// <summary>True si el nombre es solo una palabra genérica ("Artículo", "Varios"…).</summary>
    public static bool DescripcionGenerica(string nombre)
    {
        var limpio = Texto.Normalizar(nombre).Trim('.', ' ');
        return Genericas.Contains(limpio);
    }

    public bool Validar(Articulo articulo, out string? error)
    {
        if (string.IsNullOrWhiteSpace(articulo.Descripcion))
        {
            error = "Indique la descripción del artículo.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(articulo.Codigo))
        {
            error = "Indique el código del artículo o deje el campo vacío para generarlo.";
            return false;
        }

        // El SENIAT prohíbe descripciones genéricas en el documento fiscal, y el nombre del
        // artículo es lo que se imprime en cada renglón.
        if (DescripcionGenerica(articulo.Descripcion))
        {
            error = $"\"{articulo.Descripcion.Trim()}\" es una descripción genérica: la factura debe decir qué se vende " +
                    "(p. ej. \"Pintura caucho blanco 1 gal\", no \"Artículo\" ni \"Varios\").";
            return false;
        }

        if (articulo.Minimo < 0)
        {
            error = "El mínimo no puede ser negativo.";
            return false;
        }

        if (articulo.PrecioVenta < 0)
        {
            error = "El precio de venta no puede ser negativo.";
            return false;
        }

        // Un servicio existe para facturarse, y su precio no se cambia en la factura.
        if (articulo.EsServicio && articulo.PrecioVenta <= 0)
        {
            error = "Indique el precio del servicio (US$, sin IVA).";
            return false;
        }

        if (!ValidarCodigoBalanza(articulo, out error))
            return false;

        var repetido = _articulos.GetAll()
            .Where(a => a.Id != articulo.Id)
            .Any(a => string.Equals(a.Codigo.Trim(), articulo.Codigo.Trim(),
                                    StringComparison.OrdinalIgnoreCase));

        if (repetido)
        {
            error = $"Ya hay un artículo con el código {articulo.Codigo.Trim()}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// El código de la balanza (PLU) es opcional y solo de granel: dígitos, que quepan en la
    /// etiqueta, y que no lo tenga otro artículo ("123" y "00123" son el mismo).
    /// </summary>
    private bool ValidarCodigoBalanza(Articulo articulo, out string? error)
    {
        error = null;
        var plu = articulo.CodigoBalanza.Trim();
        if (plu.Length == 0)
            return true;

        if (!articulo.EsGranel)
        {
            error = "El código de balanza es solo para artículos a granel (kilogramo, litro o metro).";
            return false;
        }

        var digitos = _digitosPlu();
        if (!plu.All(char.IsAsciiDigit) || plu.Length > digitos)
        {
            error = $"El código de balanza son solo números, de hasta {digitos} dígitos (el número del producto en la balanza).";
            return false;
        }

        var normal = EtiquetaBalanza.NormalizarPlu(plu);
        var otro = _articulos.GetAll()
            .FirstOrDefault(a => a.Id != articulo.Id && a.CodigoBalanza.Trim().Length > 0
                                 && EtiquetaBalanza.NormalizarPlu(a.CodigoBalanza) == normal);
        if (otro is not null)
        {
            error = $"El código de balanza {plu} ya lo tiene {otro.Codigo} · {otro.Descripcion}.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// ¿Se puede borrar el artículo? Uno que nunca se movió, siempre. Uno con movimientos, solo
    /// si antes se desactivó y no le queda existencia.
    ///
    /// Borrar uno con movimientos no toca su historial: cada renglón de entrada, salida y factura
    /// guarda su propio snapshot de código y descripción, y el kardex suma por <c>ArticuloId</c>
    /// sin leer el catálogo. Lo que sí se pierde es lo que se lee del catálogo vivo: la categoría
    /// con que Reportes · Ventas agrupa sus ventas pasadas (salen en "Sin categoría").
    ///
    /// La existencia tiene que estar en cero porque, borrado el artículo, la mercancía que quedara
    /// seguiría contando en el kardex sin aparecer en ningún sitio para darle salida.
    /// </summary>
    public bool PuedeEliminar(Articulo articulo, out string? motivo)
    {
        var tieneMovimientos =
            _entradas.GetAll().Any(e => e.Lineas.Any(l => l.ArticuloId == articulo.Id))
            || _salidas.GetAll().Any(s => s.Lineas.Any(l => l.ArticuloId == articulo.Id));

        if (tieneMovimientos && articulo.Activo)
        {
            motivo = $"{articulo.Etiqueta} tiene movimientos en el almacén. Para eliminarlo, primero "
                     + "edítelo y desmarque \"Activo\".";
            return false;
        }

        var existencia = tieneMovimientos ? Existencia(articulo.Id) : 0m;
        if (existencia != 0)
        {
            motivo = $"{articulo.Etiqueta} todavía tiene {existencia:N2} {articulo.UnidadCorta} en existencia. "
                     + "Dele salida (por ejemplo, como merma) antes de eliminarlo.";
            return false;
        }

        motivo = null;
        return true;
    }

    /// <summary>
    /// La misma regla para un servicio (Ventas · Servicios), donde "movimiento" es haber salido
    /// en una factura: sin facturar, siempre; facturado, solo si antes se desactivó. No hay
    /// existencia que comprobar. Las facturas no se tocan: el renglón guarda su snapshot.
    /// </summary>
    public static bool PuedeEliminarServicio(Articulo servicio, bool facturado, out string? motivo)
    {
        if (facturado && servicio.Activo)
        {
            motivo = $"{servicio.Etiqueta} ya se facturó. Para eliminarlo, primero edítelo y "
                     + "desmarque \"Activo\".";
            return false;
        }

        motivo = null;
        return true;
    }

    // --- Kardex ---

    /// <summary>
    /// Existencia de cada artículo que se haya movido alguna vez: lo que entró menos lo que
    /// salió, sin contar documentos anulados.
    ///
    /// Recorre las dos tablas UNA vez cada una y suma en memoria sobre un diccionario. No
    /// consulta por artículo: hacerlo sería una consulta por fila de la grilla del almacén.
    /// </summary>
    public IReadOnlyDictionary<int, decimal> ExistenciasPorArticulo()
    {
        var saldos = new Dictionary<int, decimal>();

        foreach (var entrada in _entradas.GetAll().Where(e => e.CuentaEnKardex))
            foreach (var linea in entrada.Lineas)
                saldos[linea.ArticuloId] = saldos.GetValueOrDefault(linea.ArticuloId) + linea.Cantidad;

        foreach (var salida in _salidas.GetAll().Where(s => s.CuentaEnKardex))
            foreach (var linea in salida.Lineas)
                saldos[linea.ArticuloId] = saldos.GetValueOrDefault(linea.ArticuloId) - linea.Cantidad;

        return saldos;
    }

    public decimal Existencia(int articuloId) => ExistenciasPorArticulo().GetValueOrDefault(articuloId);

    /// <summary>
    /// Rellena <see cref="Articulo.Existencia"/> sobre los artículos que ya están en pantalla.
    ///
    /// Los modelos no avisan de sus cambios, así que quien llame a esto tiene que refrescar la
    /// vista después (<c>ItemsView.Refresh()</c>) o la grilla seguirá mostrando lo anterior.
    /// </summary>
    public void RellenarExistencias(IEnumerable<Articulo> articulos)
    {
        var saldos = ExistenciasPorArticulo();

        foreach (var articulo in articulos)
            articulo.Existencia = saldos.GetValueOrDefault(articulo.Id);
    }

    /// <summary>
    /// Artículos activos con existencia, para elegir en una entrada o una salida. Solo bienes:
    /// un servicio no entra ni sale del almacén.
    /// </summary>
    public IReadOnlyList<Articulo> ActivosConExistencia()
    {
        var articulos = _articulos.GetActivos().Where(a => !a.EsServicio).ToList();
        RellenarExistencias(articulos);
        return articulos;
    }

    /// <summary>Lo que se puede facturar: bienes activos (con su existencia) y servicios activos.</summary>
    public IReadOnlyList<Articulo> ParaVender()
    {
        var articulos = _articulos.GetActivos().ToList();
        RellenarExistencias(articulos);
        return articulos;
    }

    // --- Resúmenes para el panel del módulo (solo bienes) ---

    public int TotalArticulosActivos() => _articulos.GetAll().Count(a => a.Activo && !a.EsServicio);

    public int ArticulosBajoMinimo()
    {
        var articulos = _articulos.GetAll().Where(a => !a.EsServicio).ToList();
        RellenarExistencias(articulos);
        return articulos.Count(a => a.BajoMinimo);
    }

    public int ArticulosSinExistencia()
    {
        var articulos = _articulos.GetAll().Where(a => !a.EsServicio).ToList();
        RellenarExistencias(articulos);
        return articulos.Count(a => a.SinExistencia);
    }
}
