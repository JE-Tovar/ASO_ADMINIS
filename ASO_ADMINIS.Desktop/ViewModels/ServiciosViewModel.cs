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
/// Ventas · Servicios: lo que se factura sin mover el almacén (cambio de aceite, recarga de
/// batería…).
///
/// Un servicio es un <see cref="Articulo"/> con <see cref="TipoArticulo.Servicio"/>: comparte
/// tabla y código con los bienes para que el renglón de venta, las notas de crédito y los
/// reportes los traten igual. Esta pantalla solo muestra servicios; Almacén solo muestra bienes.
/// </summary>
public sealed class ServiciosViewModel : PantallaCrudViewModel<Articulo, int>
{
    private readonly InventarioService _inventario;
    private readonly TasaCambioService _tasaCambio;
    private readonly IVentaDataSource _ventas;
    private readonly IServicioDialogo _dialogos;

    public ServiciosViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearArticulos(),
               new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private ServiciosViewModel(Modulo modulo,
                               Submodulo submodulo,
                               IArticuloDataSource articulos,
                               IServicioDialogo dialogos,
                               ISesionActual sesion)
        : base(modulo, submodulo, articulos, dialogos, sesion)
    {
        _dialogos = dialogos;
        _inventario = new InventarioService(articulos,
                                            DataSourceFactory.CrearEntradasInventario(),
                                            DataSourceFactory.CrearSalidasInventario());
        _tasaCambio = new TasaCambioService(
            DataSourceFactory.CrearTasasCambio(), DataSourceFactory.CrearProveedorTasaCambio());
        _ventas = DataSourceFactory.CrearVentas();

        RellenarPrecioBs();
        ItemsView.Refresh();
    }

    protected override string ModuloPermiso => "Servicios";

    protected override bool CoincideBusqueda(Articulo item, string texto) =>
        item.Codigo.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Categoria.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(Articulo item) => item.EsServicio;

    /// <summary>
    /// El botón queda activo con cualquier servicio seleccionado: si no se puede borrar,
    /// <see cref="Eliminar"/> explica por qué.
    /// </summary>
    protected override bool PuedeEliminar(Articulo item) => true;

    protected override void Eliminar()
    {
        if (SelectedItem is not { } servicio)
            return;

        var facturado = _ventas.GetAll().Any(v => v.Lineas.Any(l => l.ArticuloId == servicio.Id));
        if (!InventarioService.PuedeEliminarServicio(servicio, facturado, out var motivo))
        {
            _dialogos.Informar("No se puede eliminar el servicio", motivo!);
            return;
        }

        base.Eliminar();
    }

    protected override string MensajeConfirmarEliminar(Articulo item) =>
        $"¿Eliminar {item.Etiqueta} del catálogo? Esta acción no se puede deshacer.\n\n"
        + "Las facturas y notas donde aparece se conservan tal cual, con su código y descripción.";

    protected override Articulo CrearNuevo() =>
        new() { Activo = true, Tipo = TipoArticulo.Servicio, Categoria = "Servicios" };

    protected override CrudEditorViewModelBase<Articulo> CrearEditor(Articulo item) =>
        new ServicioEditorViewModel(item, _inventario, _tasaCambio.Actual()?.Valor,
            [.. Items.Where(a => a.EsServicio).Select(a => a.Categoria)]);

    public override void Recargar()
    {
        base.Recargar();
        RellenarPrecioBs();
        ItemsView.Refresh();
        OnTodasLasPropiedadesCambiaron();
    }

    private void RellenarPrecioBs()
    {
        var tasa = _tasaCambio.Actual()?.Valor ?? 0m;
        foreach (var servicio in Items)
            servicio.PrecioVentaBs = tasa > 0 ? servicio.PrecioVenta * tasa : 0m;
    }
}

/// <summary>
/// Alta/edición de un servicio: lo justo para facturarlo. No pide unidad, mínimo, ubicación ni
/// presentaciones porque un servicio no tiene existencia. Las reglas (código único, descripción
/// no genérica, precio mayor que cero) son las de <see cref="InventarioService.Validar"/>.
/// </summary>
public sealed class ServicioEditorViewModel : CrudEditorViewModelBase<Articulo>
{
    private readonly Articulo _original;
    private readonly InventarioService _servicio;
    private readonly decimal? _tasaCambio;

    public ServicioEditorViewModel(Articulo original,
                                   InventarioService servicio,
                                   decimal? tasaCambio,
                                   IEnumerable<string> categoriasUsadas)
    {
        _original = original;
        _servicio = servicio;
        _tasaCambio = tasaCambio;

        Codigo = original.Codigo;
        Descripcion = original.Descripcion;
        Categoria = original.Categoria;
        Notas = original.Notas;
        Activo = original.Id == 0 || original.Activo;
        PrecioVenta = original.PrecioVenta == 0 ? string.Empty : original.PrecioVenta.ToString("0.##");
        AlicuotaSeleccionada = original.Alicuota;

        Categorias = [.. categoriasUsadas.Append("Servicios")
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)];

        GenerarCodigoCommand = new RelayCommand(() => Codigo = _servicio.GenerarCodigo());
    }

    public override string Titulo =>
        _original.Id == 0 ? "Nuevo servicio" : $"Editar {_original.Codigo} · {_original.Descripcion}";

    public ICommand GenerarCodigoCommand { get; }

    public IReadOnlyList<string> Categorias { get; }

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

    public string PrecioVentaBsTexto => _tasaCambio is { } tasa && decimal.TryParse(PrecioVenta, out var precio) && precio > 0
        ? $"≈ Bs. {(precio * tasa):N2}"
        : _tasaCambio is null
            ? "Sin tasa de cambio registrada todavía."
            : string.Empty;

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
        if (!decimal.TryParse(PrecioVenta, out var precio) || precio <= 0)
        {
            error = "Indique el precio del servicio: un número mayor que cero (US$, sin IVA).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Codigo))
            Codigo = _servicio.GenerarCodigo();

        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override Articulo ObtenerResultado()
    {
        var articulo = _original.Clonar();
        articulo.Tipo = TipoArticulo.Servicio;
        articulo.Codigo = Codigo.Trim();
        articulo.Descripcion = Descripcion.Trim();
        articulo.Categoria = Categoria.Trim();
        articulo.Alicuota = AlicuotaSeleccionada;
        articulo.PrecioVenta = decimal.TryParse(PrecioVenta, out var precio) ? precio : 0m;
        articulo.Notas = Notas.Trim();
        articulo.Activo = Activo;

        // Sin unidad, mínimo ni ubicación: no se mide ni se guarda.
        articulo.Unidad = UnidadMedida.Pieza;
        articulo.Minimo = 0m;
        articulo.Ubicacion = string.Empty;
        return articulo;
    }
}
