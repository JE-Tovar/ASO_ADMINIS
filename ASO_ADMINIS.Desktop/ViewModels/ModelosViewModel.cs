using System;
using System.Linq;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Catálogo · Modelos: lo que la organización vende de cada marca, con su existencia ya
/// conectada a Inventario.
///
/// El alta y la edición pasan por <see cref="CatalogoService"/> y no por la fuente de datos: dar
/// de alta un modelo es también crear su artículo en Inventario, y eso no lo sabe el CRUD
/// genérico — mismo criterio que <see cref="EntradasViewModel"/> con
/// <c>EntradasInventarioService</c>.
/// </summary>
public sealed class ModelosViewModel : PantallaCrudViewModel<Modelo, int>
{
    private const string FiltroTodas = "Todas";

    private readonly IMarcaDataSource _marcas;
    private readonly IServicioDialogo _dialogos;
    private readonly CatalogoService _servicio;

    private string _filtro = FiltroTodas;

    public ModelosViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearModelos(),
               new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private ModelosViewModel(Modulo modulo,
                             Submodulo submodulo,
                             IModeloDataSource modelos,
                             IServicioDialogo dialogos,
                             ISesionActual sesion)
        : base(modulo, submodulo, modelos, dialogos, sesion)
    {
        _dialogos = dialogos;
        _marcas = DataSourceFactory.CrearMarcas();

        var articulos = DataSourceFactory.CrearArticulos();
        var inventario = new InventarioService(articulos,
                                               DataSourceFactory.CrearEntradasInventario(),
                                               DataSourceFactory.CrearSalidasInventario());

        _servicio = new CatalogoService(_marcas, modelos, articulos, inventario);

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });
    }

    public ICommand CambiarFiltroCommand { get; }

    protected override string ModuloPermiso => "Modelos";

    protected override bool CoincideBusqueda(Modelo item, string texto) =>
        item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.MarcaNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.CategoriaTexto.Contains(texto, StringComparison.OrdinalIgnoreCase);

    /// <summary>Secciones de Modelos: la misma grilla, filtrada por categoría de producto.</summary>
    protected override bool PasaFiltroExtra(Modelo item) => _filtro switch
    {
        "Calzado" => item.Categoria == CategoriaProducto.Calzado,
        "Ropa" => item.Categoria == CategoriaProducto.Ropa,
        "Correas y carteras" => item.Categoria == CategoriaProducto.CorreasYCarteras,
        "Otro" => item.Categoria == CategoriaProducto.Otro,
        _ => true
    };

    protected override bool PuedeEliminar(Modelo item) => _servicio.PuedeEliminarModelo(item);

    protected override Modelo CrearNuevo() => new() { Activo = true };

    protected override CrudEditorViewModelBase<Modelo> CrearEditor(Modelo item) =>
        new ModeloEditorViewModel(item, [.. _marcas.GetAll().Where(m => m.Activo).OrderBy(m => m.Nombre)],
                                  _servicio, _marcas, _dialogos);

    protected override void Agregar()
    {
        var editor = CrearEditor(CrearNuevo());

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.RegistrarModelo(editor.ObtenerResultado()));
    }

    protected override void Editar()
    {
        if (SelectedItem is not { } actual)
            return;

        var editor = CrearEditor(actual);

        if (!_dialogos.MostrarEditor(editor))
            return;

        var actualizado = editor.ObtenerResultado();
        Aplicar(() =>
        {
            _servicio.ActualizarModelo(actualizado);
            return actualizado;
        });
    }

    /// <summary>Elimina también el artículo vinculado (ver <see cref="CatalogoService.EliminarModelo"/>).</summary>
    protected override void Eliminar()
    {
        if (SelectedItem is not { } actual)
            return;

        if (!_dialogos.Confirmar("Eliminar", "¿Eliminar el modelo seleccionado? Esta acción no se puede deshacer."))
            return;

        _servicio.EliminarModelo(actual);
        SelectedItem = default;
    }

    private void Aplicar(Func<Modelo> operacion)
    {
        try
        {
            SeleccionarTrasRecargar(operacion().Id);
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudo completar la operación", ex.Message);
        }
    }
}
