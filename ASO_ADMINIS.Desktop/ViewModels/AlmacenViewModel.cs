using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private readonly TasaCambioService _tasaCambio;
    private readonly IServicioDialogo _dialogos;

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
        _dialogos = dialogos;
        _servicio = new InventarioService(articulos,
                                          DataSourceFactory.CrearEntradasInventario(),
                                          DataSourceFactory.CrearSalidasInventario());

        _tasaCambio = new TasaCambioService(
            DataSourceFactory.CrearTasasCambio(), DataSourceFactory.CrearProveedorTasaCambio());

        // La base ya pobló Items en su constructor, pero sin existencias ni precio en Bs: sin
        // esto la primera pintada saldría con todo en cero hasta la primera recarga.
        RellenarDerivados();
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
        $"{Items.Count(a => a.Activo && !a.EsServicio)} artículos activos · {Items.Count(a => a.BajoMinimo)} bajo mínimo";

    protected override string ModuloPermiso => "Articulos";

    protected override bool CoincideBusqueda(Articulo item, string texto) =>
        item.Codigo.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Categoria.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Ubicacion.Contains(texto, StringComparison.OrdinalIgnoreCase);

    /// <summary>Los servicios comparten tabla pero viven en Ventas · Servicios, no aquí.</summary>
    protected override bool PasaFiltroExtra(Articulo item) => !item.EsServicio && _filtro switch
    {
        "Bajo mínimo" => item.BajoMinimo,
        "Sin existencia" => item.SinExistencia,
        "Inactivos" => !item.Activo,
        _ => true
    };

    /// <summary>
    /// El botón queda activo con cualquier artículo seleccionado: si no se puede borrar,
    /// <see cref="Eliminar"/> explica por qué en vez de dejarlo gris sin decir nada.
    /// </summary>
    protected override bool PuedeEliminar(Articulo item) => true;

    protected override void Eliminar()
    {
        if (SelectedItem is not { } articulo)
            return;

        if (!_servicio.PuedeEliminar(articulo, out var motivo))
        {
            _dialogos.Informar("No se puede eliminar el artículo", motivo!);
            return;
        }

        base.Eliminar();
    }

    protected override string MensajeConfirmarEliminar(Articulo item) =>
        $"¿Eliminar {item.Etiqueta} del catálogo? Esta acción no se puede deshacer.\n\n"
        + "Las entradas, salidas y facturas donde aparece se conservan tal cual, con su código y descripción.";

    protected override Articulo CrearNuevo() => new() { Activo = true };

    protected override CrudEditorViewModelBase<Articulo> CrearEditor(Articulo item) =>
        new ArticuloEditorViewModel(item, _servicio, _tasaCambio.Actual()?.Valor);

    /// <summary>
    /// Relee el catálogo y le vuelve a pegar la existencia encima. El refresco va después de
    /// rellenar y no antes: el filtro "bajo mínimo" depende justamente de ese número.
    /// </summary>
    public override void Recargar()
    {
        base.Recargar();

        RellenarDerivados();
        ItemsView.Refresh();
        OnTodasLasPropiedadesCambiaron();
    }

    /// <summary>Lo que no está en la tabla: existencia (del kardex) y precio en Bs (de la tasa vigente).</summary>
    private void RellenarDerivados()
    {
        _servicio.RellenarExistencias(Items);

        var tasa = _tasaCambio.Actual()?.Valor ?? 0m;
        foreach (var articulo in Items)
            articulo.PrecioVentaBs = tasa > 0 ? articulo.PrecioVenta * tasa : 0m;
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
    private readonly decimal? _tasaCambio;

    public ArticuloEditorViewModel(Articulo original, InventarioService servicio, decimal? tasaCambio)
    {
        _original = original;
        _servicio = servicio;
        _tasaCambio = tasaCambio;

        Codigo = original.Codigo;
        Descripcion = original.Descripcion;
        Categoria = original.Categoria;
        Ubicacion = original.Ubicacion;
        Notas = original.Notas;
        CodigoBalanza = original.CodigoBalanza;
        Activo = original.Id == 0 || original.Activo;
        Minimo = original.Minimo == 0 ? string.Empty : original.Minimo.ToString("0.##");
        PrecioVenta = original.PrecioVenta == 0 ? string.Empty : original.PrecioVenta.ToString("0.##");
        UnidadSeleccionada = original.Unidad;
        AlicuotaSeleccionada = original.Alicuota;

        GenerarCodigoCommand = new RelayCommand(() => Codigo = _servicio.GenerarCodigo());
    }

    public override string Titulo =>
        _original.Id == 0 ? "Nuevo artículo" : $"Editar {_original.Codigo} · {_original.Descripcion}";

    /// <summary>Amplio: precio, alícuota y unidad van en una fila de tres columnas.</summary>
    public override double AnchoEditor => Ancho.Amplio;

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
        set
        {
            if (SetProperty(ref _unidadSeleccionada, value))
                OnPropertyChanged(nameof(EsGranel));
        }
    }

    public IReadOnlyList<OpcionAlicuota> OpcionesAlicuota { get; } = OpcionAlicuota.Todas;

    private TipoAlicuota _alicuotaSeleccionada = TipoAlicuota.General;
    public TipoAlicuota AlicuotaSeleccionada
    {
        get => _alicuotaSeleccionada;
        set => SetProperty(ref _alicuotaSeleccionada, value);
    }

    private string _codigo = string.Empty;
    public string Codigo
    {
        get => _codigo;
        set => SetProperty(ref _codigo, value);
    }

    private string _descripcion = string.Empty;
    public string Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
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

    private string _precioVenta = string.Empty;
    public string PrecioVenta
    {
        get => _precioVenta;
        set
        {
            if (SetProperty(ref _precioVenta, value))
                OnPropertyChanged(nameof(PrecioVentaBsTexto));
        }
    }

    /// <summary>Vista previa en vivo del equivalente en bolívares, con la tasa vigente al abrir el editor.</summary>
    public string PrecioVentaBsTexto => _tasaCambio is { } tasa && decimal.TryParse(PrecioVenta, out var precio) && precio > 0
        ? $"≈ Bs. {(precio * tasa):N2}"
        : _tasaCambio is null
            ? "Sin tasa de cambio registrada todavía."
            : string.Empty;

    /// <summary>Kilogramo, litro o metro: se vende con decimales y puede pesarse en balanza.</summary>
    public bool EsGranel => new Articulo { Unidad = UnidadSeleccionada }.EsGranel;

    private string _codigoBalanza = string.Empty;
    /// <summary>Número del producto en la balanza (PLU), el que trae su etiqueta. Opcional.</summary>
    public string CodigoBalanza
    {
        get => _codigoBalanza;
        set => SetProperty(ref _codigoBalanza, value);
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

        if (!string.IsNullOrWhiteSpace(PrecioVenta)
            && (!decimal.TryParse(PrecioVenta, out var precio) || precio < 0))
        {
            error = "El precio de venta debe ser un número mayor o igual a cero.";
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
        articulo.Descripcion = Descripcion.Trim();
        articulo.Categoria = Categoria.Trim();
        articulo.Unidad = UnidadSeleccionada;
        articulo.Alicuota = AlicuotaSeleccionada;
        articulo.Minimo = decimal.TryParse(Minimo, out var minimo) ? minimo : 0m;
        articulo.PrecioVenta = decimal.TryParse(PrecioVenta, out var precio) ? precio : 0m;
        articulo.Ubicacion = Ubicacion.Trim();
        articulo.Notas = Notas.Trim();
        articulo.Activo = Activo;
        // Fuera de granel no aplica: se descarta en vez de guardar un PLU que nunca se leería.
        articulo.CodigoBalanza = articulo.EsGranel ? CodigoBalanza.Trim() : string.Empty;

        return articulo;
    }
}
