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

    public InventarioService(IArticuloDataSource articulos,
                             IEntradaInventarioDataSource entradas,
                             ISalidaInventarioDataSource salidas)
    {
        _articulos = articulos;
        _entradas = entradas;
        _salidas = salidas;
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

    public bool Validar(Articulo articulo, out string? error)
    {
        if (string.IsNullOrWhiteSpace(articulo.Nombre))
        {
            error = "Indique el nombre del artículo.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(articulo.Codigo))
        {
            error = "Indique el código del artículo o deje el campo vacío para generarlo.";
            return false;
        }

        if (articulo.Minimo < 0)
        {
            error = "El mínimo no puede ser negativo.";
            return false;
        }

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
    /// Un artículo que ya se movió no se borra: se desactiva. Borrarlo dejaría las líneas de los
    /// documentos apuntando a un artículo que no existe y el kardex sin poder cuadrarse.
    /// </summary>
    public bool PuedeEliminar(Articulo articulo)
        => !_entradas.GetAll().Any(e => e.Lineas.Any(l => l.ArticuloId == articulo.Id))
           && !_salidas.GetAll().Any(s => s.Lineas.Any(l => l.ArticuloId == articulo.Id));

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

    /// <summary>Artículos activos con existencia, para elegir en una entrada o una salida.</summary>
    public IReadOnlyList<Articulo> ActivosConExistencia()
    {
        var articulos = _articulos.GetActivos().ToList();
        RellenarExistencias(articulos);
        return articulos;
    }

    // --- Resúmenes para el panel del módulo ---

    public int TotalArticulosActivos() => _articulos.GetAll().Count(a => a.Activo);

    public int ArticulosBajoMinimo()
    {
        var articulos = _articulos.GetAll().ToList();
        RellenarExistencias(articulos);
        return articulos.Count(a => a.BajoMinimo);
    }

    public int ArticulosSinExistencia()
    {
        var articulos = _articulos.GetAll().ToList();
        RellenarExistencias(articulos);
        return articulos.Count(a => a.SinExistencia);
    }
}
