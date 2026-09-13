using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Inventario · Almacén: el catálogo de artículos con su existencia al día.
///
/// Es un maestro CRUD normal con una vuelta: la existencia no está en la tabla, se calcula del
/// kardex. Como los modelos no avisan de sus cambios, cada vez que se rellena hay que refrescar
/// la vista a mano — ver <see cref="Recargar"/>.
/// </summary>
public sealed class AlmacenViewModel : PantallaCrudViewModel<Articulo, int>
{
    private const string FiltroTodos = "Todos";

    private readonly InventarioService _servicio;

    private string _filtro = FiltroTodos;

    public AlmacenViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearArticulos(),
               new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private AlmacenViewModel(Modulo modulo,
                             Submodulo submodulo,
                             IArticuloDataSource articulos,
                             IServicioDialogo dialogos,
                             ISesionActual sesion)
        : base(modulo, submodulo, articulos, dialogos, sesion)
    {
        _servicio = new InventarioService(articulos,
                                          DataSourceFactory.CrearEntradasInventario(),
                                          DataSourceFactory.CrearSalidasInventario());

        // La base ya pobló Items en su constructor, pero sin existencias: sin esto la primera
        // pintada saldría con todo en cero hasta la primera recarga.
        _servicio.RellenarExistencias(Items);
        ItemsView.Refresh();

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });
    }

    public ICommand CambiarFiltroCommand { get; }

    /// <summary>Estado del almacén de un vistazo, sin ir al resumen del módulo.</summary>
    public string Resumen =>
        $"{Items.Count(a => a.Activo)} artículos activos · {Items.Count(a => a.BajoMinimo)} bajo mínimo";

    protected override string ModuloPermiso => "Articulos";

    protected override bool CoincideBusqueda(Articulo item, string texto) =>
        item.Codigo.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Categoria.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Ubicacion.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(Articulo item) => _filtro switch
    {
        "Bajo mínimo" => item.BajoMinimo,
        "Sin existencia" => item.SinExistencia,
        "Inactivos" => !item.Activo,
        _ => true
    };

    /// <summary>Un artículo que ya se movió no se borra, se desactiva.</summary>
    protected override bool PuedeEliminar(Articulo item) => _servicio.PuedeEliminar(item);

    protected override Articulo CrearNuevo() => new() { Activo = true };

    protected override CrudEditorViewModelBase<Articulo> CrearEditor(Articulo item) =>
        new ArticuloEditorViewModel(item, _servicio);

    /// <summary>
    /// Relee el catálogo y le vuelve a pegar la existencia encima. El refresco va después de
    /// rellenar y no antes: el filtro "bajo mínimo" depende justamente de ese número.
    /// </summary>
    public override void Recargar()
    {
        base.Recargar();

        _servicio.RellenarExistencias(Items);
        ItemsView.Refresh();
        OnTodasLasPropiedadesCambiaron();
    }
}

/// <summary>
/// Alta/edición de un artículo.
///
/// El código puede escribirse a mano o dejarse en blanco: si se deja, lo genera
/// <see cref="InventarioService.GenerarCodigo"/> al validar y se escribe en el campo, para que
/// quien lo dio de alta lo vea antes de que la ventana se cierre.
/// </summary>
public sealed class ArticuloEditorViewModel : CrudEditorViewModelBase<Articulo>
{
    private readonly Articulo _original;
    private readonly InventarioService _servicio;

    public ArticuloEditorViewModel(Articulo original, InventarioService servicio)
    {
        _original = original;
        _servicio = servicio;

        Codigo = original.Codigo;
        Nombre = original.Nombre;
        Categoria = original.Categoria;
        Ubicacion = original.Ubicacion;
        Notas = original.Notas;
        Activo = original.Id == 0 || original.Activo;
        Minimo = original.Minimo == 0 ? string.Empty : original.Minimo.ToString("0.##");
        UnidadSeleccionada = original.Unidad;

        GenerarCodigoCommand = new RelayCommand(() => Codigo = _servicio.GenerarCodigo());
    }

    public override string Titulo =>
        _original.Id == 0 ? "Nuevo artículo" : $"Editar {_original.Nombre}";

    public override double AnchoEditor => Ancho.Estandar;

    public ICommand GenerarCodigoCommand { get; }

    public IReadOnlyList<UnidadMedida> Unidades { get; } =
    [
        UnidadMedida.Pieza, UnidadMedida.Caja, UnidadMedida.Paleta, UnidadMedida.Kilogramo,
        UnidadMedida.Litro, UnidadMedida.Metro, UnidadMedida.Rollo, UnidadMedida.Saco,
        UnidadMedida.Par
    ];

    private UnidadMedida _unidadSeleccionada;
    public UnidadMedida UnidadSeleccionada
    {
        get => _unidadSeleccionada;
        set => SetProperty(ref _unidadSeleccionada, value);
    }

    private string _codigo = string.Empty;
    public string Codigo
    {
        get => _codigo;
        set => SetProperty(ref _codigo, value);
    }

    private string _nombre = string.Empty;
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

    private string _categoria = string.Empty;
    public string Categoria
    {
        get => _categoria;
        set => SetProperty(ref _categoria, value);
    }

    private string _minimo = string.Empty;
    public string Minimo
    {
        get => _minimo;
        set => SetProperty(ref _minimo, value);
    }

    private string _ubicacion = string.Empty;
    public string Ubicacion
    {
        get => _ubicacion;
        set => SetProperty(ref _ubicacion, value);
    }

    private string _notas = string.Empty;
    public string Notas
    {
        get => _notas;
        set => SetProperty(ref _notas, value);
    }

    private bool _activo = true;
    public bool Activo
    {
        get => _activo;
        set => SetProperty(ref _activo, value);
    }

    protected override bool Validar(out string? error)
    {
        if (!string.IsNullOrWhiteSpace(Minimo)
            && (!decimal.TryParse(Minimo, out var minimo) || minimo < 0))
        {
            error = "El mínimo debe ser un número mayor o igual a cero.";
            return false;
        }

        // Se genera ANTES de validar y se deja escrito en el campo: así el usuario ve con qué
        // código se guardó, en vez de enterarse al volver al listado.
        if (string.IsNullOrWhiteSpace(Codigo))
            Codigo = _servicio.GenerarCodigo();

        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override Articulo ObtenerResultado()
    {
        var articulo = _original.Clonar();
        articulo.Codigo = Codigo.Trim();
        articulo.Nombre = Nombre.Trim();
        articulo.Categoria = Categoria.Trim();
        articulo.Unidad = UnidadSeleccionada;
        articulo.Minimo = decimal.TryParse(Minimo, out var minimo) ? minimo : 0m;
        articulo.Ubicacion = Ubicacion.Trim();
        articulo.Notas = Notas.Trim();
        articulo.Activo = Activo;
        return articulo;
    }
}
