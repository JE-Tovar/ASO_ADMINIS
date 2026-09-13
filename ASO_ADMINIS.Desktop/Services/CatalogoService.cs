using System.Linq;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Reglas del catálogo de Modelos y su vínculo con Inventario.
///
/// Un Modelo no lleva existencia propia: la lleva el <see cref="Articulo"/> que este servicio le
/// crea al darlo de alta (mismo acoplamiento entre módulos que <c>EntradasInventarioService</c>
/// exigiendo <c>CuentasPorPagarService</c> — ver CLAUDE.md, sección Inventario). Código, Unidad,
/// Mínimo y Ubicación del artículo vinculado siguen siendo terreno exclusivo de Almacén: este
/// servicio solo sincroniza lo que es del Modelo (Nombre, Categoría, Activo).
///
/// Marca es un maestro CRUD simple sin servicio propio (arquetipo <c>Proveedor</c>): su
/// validación vive en <c>MarcaEditorViewModel</c>, igual que la de un proveedor vive en
/// <c>ProveedorEditorViewModel</c>.
/// </summary>
public sealed class CatalogoService
{
    private readonly IMarcaDataSource _marcas;
    private readonly IModeloDataSource _modelos;
    private readonly IArticuloDataSource _articulos;
    private readonly InventarioService _inventario;

    public CatalogoService(IMarcaDataSource marcas,
                           IModeloDataSource modelos,
                           IArticuloDataSource articulos,
                           InventarioService inventario)
    {
        _marcas = marcas;
        _modelos = modelos;
        _articulos = articulos;
        _inventario = inventario;
    }

    public bool Validar(Modelo modelo, out string? error)
    {
        if (string.IsNullOrWhiteSpace(modelo.Nombre))
        {
            error = "Indique el nombre del modelo.";
            return false;
        }

        if (_marcas.GetById(modelo.MarcaId) is not { Activo: true })
        {
            error = "Elija una marca activa.";
            return false;
        }

        if (modelo.PrecioVenta < 0)
        {
            error = "El precio de venta no puede ser negativo.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Da de alta el modelo y su artículo vinculado en Inventario. El artículo nace sin mínimo
    /// ni ubicación: eso lo completa después quien mantenga el almacén, en Inventario · Almacén.
    /// </summary>
    public Modelo RegistrarModelo(Modelo modelo)
    {
        var guardado = _modelos.Add(modelo);

        _articulos.Add(new Articulo
        {
            ModeloId = guardado.Id,
            Codigo = _inventario.GenerarCodigo(),
            Nombre = guardado.Nombre,
            Categoria = guardado.CategoriaTexto,
            Unidad = guardado.Unidad,
            Activo = guardado.Activo
        });

        return guardado;
    }

    /// <summary>
    /// Guarda los cambios del modelo y sincroniza Nombre/Categoría/Activo en su artículo
    /// vinculado.
    /// </summary>
    public void ActualizarModelo(Modelo modelo)
    {
        _modelos.Update(modelo);

        if (BuscarArticuloDe(modelo.Id) is { } articulo)
        {
            articulo.Nombre = modelo.Nombre;
            articulo.Categoria = modelo.CategoriaTexto;
            articulo.Activo = modelo.Activo;
            _articulos.Update(articulo);
        }
    }

    /// <summary>Un modelo cuyo artículo ya se movió no se borra: se desactiva.</summary>
    public bool PuedeEliminarModelo(Modelo modelo)
        => BuscarArticuloDe(modelo.Id) is not { } articulo || _inventario.PuedeEliminar(articulo);

    /// <summary>Borra el modelo y, si existe, su artículo vinculado (ver <see cref="PuedeEliminarModelo"/>).</summary>
    public void EliminarModelo(Modelo modelo)
    {
        if (BuscarArticuloDe(modelo.Id) is { } articulo)
            _articulos.Delete(articulo.Id);

        _modelos.Delete(modelo.Id);
    }

    private Articulo? BuscarArticuloDe(int modeloId)
        => _articulos.GetAll().FirstOrDefault(a => a.ModeloId == modeloId);
}
