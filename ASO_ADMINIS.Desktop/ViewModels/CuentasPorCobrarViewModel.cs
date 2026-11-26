using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

public sealed class CuentasPorCobrarViewModel : PantallaCrudViewModel<CuentaPorCobrar, int>
{
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesion;
    private readonly CuentasPorCobrarService _servicio;
    private string _filtroEstado = "Todas";

    public CuentasPorCobrarViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearCuentasPorCobrar(),
               DataSourceFactory.CrearServicioCuentasPorCobrar(), new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private CuentasPorCobrarViewModel(Modulo modulo, Submodulo submodulo, ICuentaPorCobrarDataSource fuente,
                                      CuentasPorCobrarService servicio, IServicioDialogo dialogos, ISesionActual sesion)
        : base(modulo, submodulo, fuente, dialogos, sesion)
    {
        _dialogos = dialogos;
        _sesion = sesion;
        _servicio = servicio;
        CambiarFiltroEstadoCommand = new RelayCommand<string>(filtro =>
        {
            _filtroEstado = filtro;
            ItemsView.Refresh();
        });
        RegistrarCobroCommand = new RelayCommand(RegistrarCobro,
            () => SelectedItem is { Estado: EstadoCuentaPorCobrar.Pendiente, SaldoBs: > 0 }
                  && _sesion.Puede(Permisos.CuentasPorCobrar.Cobrar));
    }

    public ICommand CambiarFiltroEstadoCommand { get; }
    public ICommand RegistrarCobroCommand { get; }
    public string ResumenDeuda => $"Por cobrar Bs {_servicio.TotalPorCobrar():N2} · vencido Bs {_servicio.TotalVencido():N2} · saldo a favor Bs {_servicio.TotalSaldoAFavor():N2}";

    protected override string ModuloPermiso => "CuentasPorCobrar";
    protected override bool PuedeEditar(CuentaPorCobrar item) => false;
    protected override bool PuedeEliminar(CuentaPorCobrar item) => false;
    protected override CuentaPorCobrar CrearNuevo() => throw new NotSupportedException("Las cuentas por cobrar nacen de una factura a crédito.");
    protected override CrudEditorViewModelBase<CuentaPorCobrar> CrearEditor(CuentaPorCobrar item) =>
        throw new NotSupportedException("Las cuentas por cobrar no se editan manualmente.");
    protected override CrudEditorViewModelBase CrearDetalle(CuentaPorCobrar item) =>
        new CuentaPorCobrarDetalleViewModel(item, _servicio, _dialogos, _sesion);

    protected override bool CoincideBusqueda(CuentaPorCobrar item, string texto) =>
        item.NumeroFactura.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.ClienteNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.ClienteRif.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(CuentaPorCobrar item) => _filtroEstado switch
    {
        "Pendientes" => item.Estado == EstadoCuentaPorCobrar.Pendiente,
        "Vencidas" => item.EstaVencida,
        "Cobradas" => item.Estado == EstadoCuentaPorCobrar.Cobrada,
        "Saldo a favor" => item.Estado == EstadoCuentaPorCobrar.SaldoAFavor,
        _ => true
    };

    private void RegistrarCobro()
    {
        if (SelectedItem is not { } cuenta)
            return;
        var editor = new CobroClienteEditorViewModel(cuenta, _servicio);
        if (!_dialogos.MostrarEditor(editor))
            return;
        try
        {
            var cobro = _servicio.RegistrarCobro(cuenta, editor.ObtenerResultado(), _sesion.UsuarioActual?.Id ?? 0);
            SeleccionarTrasRecargar(cobro.CuentaPorCobrarId);
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudo registrar el cobro", ex.Message);
        }
    }
}
