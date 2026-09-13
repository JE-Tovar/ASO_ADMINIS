using System;
using System.Linq;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Ventas · Registro: el historial de ventas y el registro de una nueva.
///
/// Misma forma que Entradas/Salidas: las filas son documentos, no se editan ni se borran, se
/// anulan. El alta pasa por <see cref="VentasService"/> y no por la fuente de datos: registrar
/// una venta también descuenta el almacén, y eso no lo sabe el CRUD genérico.
/// </summary>
public sealed class VentasViewModel : PantallaCrudViewModel<Venta, int>
{
    private const string FiltroTodas = "Todas";

    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;
    private readonly InventarioService _inventario;
    private readonly VentasService _servicio;
    private readonly TasaCambioService _tasaCambio;

    private string _filtro = FiltroTodas;

    public VentasViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearVentas(), new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private VentasViewModel(Modulo modulo,
                            Submodulo submodulo,
                            IVentaDataSource ventas,
                            IServicioDialogo dialogos,
                            ISesionActual sesion)
        : base(modulo, submodulo, ventas, dialogos, sesion)
    {
        _dialogos = dialogos;
        _sesionActual = sesion;

        var articulos = DataSourceFactory.CrearArticulos();
        _inventario = new InventarioService(articulos,
                                            DataSourceFactory.CrearEntradasInventario(),
                                            DataSourceFactory.CrearSalidasInventario());

        _servicio = new VentasService(ventas, articulos, DataSourceFactory.CrearSalidasInventario(),
                                      _inventario, sesion);

        _tasaCambio = new TasaCambioService(
            DataSourceFactory.CrearTasasCambio(), DataSourceFactory.CrearProveedorTasaCambio());

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } v && _servicio.PuedeAnular(v)
                  && _sesionActual.Puede(Permisos.Ventas.Anular));
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand AnularCommand { get; }

    public string Resumen
    {
        get
        {
            var hoy = DateTime.Today;
            var delMes = Items.Where(v => v.Estado == EstadoVenta.Registrada
                                          && v.Fecha.Year == hoy.Year && v.Fecha.Month == hoy.Month);
            return $"{Items.Count(v => v.Estado == EstadoVenta.Registrada)} ventas · " +
                   $"{delMes.Sum(v => v.TotalUsd):N2} US$ este mes";
        }
    }

    protected override string ModuloPermiso => "Ventas";

    protected override bool CoincideBusqueda(Venta item, string texto) =>
        item.Numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.ModeloNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.MarcaNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.ClienteNombre.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(Venta item) => _filtro switch
    {
        "Anuladas" => item.Estado == EstadoVenta.Anulada,
        _ => true
    };

    protected override bool PuedeEditar(Venta item) => false;

    protected override bool PuedeEliminar(Venta item) => false;

    protected override Venta CrearNuevo() => new()
    {
        Fecha = DateTime.Today,
        Estado = EstadoVenta.Registrada
    };

    /// <summary>
    /// Los modelos y su existencia se leen frescos en cada apertura del editor, no se cachean:
    /// mismo criterio que <c>SalidasViewModel.CrearEditor</c> con
    /// <c>InventarioService.ActivosConExistencia</c>.
    /// </summary>
    protected override CrudEditorViewModelBase<Venta> CrearEditor(Venta item)
    {
        var modelos = DataSourceFactory.CrearModelos().GetAll()
            .Where(m => m.Activo)
            .OrderBy(m => m.Nombre)
            .ToList();

        var articulosPorModelo = _inventario.ActivosConExistencia()
            .Where(a => a.ModeloId.HasValue)
            .ToDictionary(a => a.ModeloId!.Value);

        return new VentaEditorViewModel(item, modelos, articulosPorModelo, _servicio, _tasaCambio.Actual()?.Valor);
    }

    /// <summary>
    /// El alta pasa por el servicio de dominio: registrar la venta es también descontar el
    /// almacén, y eso no lo sabe el CRUD genérico.
    /// </summary>
    protected override void Agregar()
    {
        var editor = CrearEditor(CrearNuevo());

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Registrar(editor.ObtenerResultado(),
                                          _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void Anular()
    {
        if (SelectedItem is not { } venta)
            return;

        var editor = new MotivoEditorViewModel(
            $"Anular venta {venta.Numero}",
            $"{venta.ModeloTexto} — {venta.CantidadTexto} — {venta.TotalUsdTexto} US$",
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(venta, editor.Motivo));
    }

    private void Aplicar(Func<Venta> transicion)
    {
        try
        {
            SeleccionarTrasRecargar(transicion().Id);
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudo completar la operación", ex.Message);
        }
    }
}
