using System;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Facturas de compra pendientes de pago. Sub-listado de Finanzas · Cuentas por Pagar; el
/// encabezado y el conmutador los pone <see cref="CuentasPorPagarViewModel"/>.
/// </summary>
public sealed class FacturasProveedorCrudViewModel : CrudViewModelBase<FacturaProveedor, int>
{
    private const string FiltroTodas = "Todas";

    private readonly IProveedorDataSource _proveedores;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;
    private readonly CuentasPorPagarService _servicio;
    private readonly BancoService _banco;

    private string _filtroEstado = FiltroTodas;

    public FacturasProveedorCrudViewModel(IFacturaProveedorDataSource facturas,
                                          IProveedorDataSource proveedores,
                                          IServicioDialogo dialogos,
                                          ISesionActual sesion)
        : base(facturas, dialogos, sesion)
    {
        _proveedores = proveedores;
        _dialogos = dialogos;
        _sesionActual = sesion;
        _banco = new BancoService(DataSourceFactory.CrearMovimientosBanco(),
                                  DataSourceFactory.CrearCuentasBancarias(), sesion);
        _servicio = new CuentasPorPagarService(facturas, _banco, sesion);

        CambiarFiltroEstadoCommand = new RelayCommand<string>(filtro =>
        {
            _filtroEstado = filtro;
            ItemsView.Refresh();
        });

        RegistrarPagoCommand = new RelayCommand(RegistrarPago,
            () => SelectedItem is { } f && _servicio.PuedeRegistrarPago(f) && _sesionActual.Puede("Finanzas.Pagar"));

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } f && _servicio.PuedeAnular(f) && _sesionActual.Puede("Finanzas.Anular"));
    }

    public ICommand CambiarFiltroEstadoCommand { get; }
    public ICommand RegistrarPagoCommand { get; }
    public ICommand AnularCommand { get; }

    /// <summary>Estado de la deuda, visible sin ir al dashboard del módulo.</summary>
    public string ResumenDeuda =>
        $"Por pagar {_servicio.TotalPorPagar():N2} · vencido {_servicio.TotalVencido():N2}";

    protected override string ModuloPermiso => "FacturasProveedor";

    protected override bool CoincideBusqueda(FacturaProveedor item, string texto) =>
        item.ProveedorNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.NumeroDocumento.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(FacturaProveedor item) => _filtroEstado switch
    {
        "Pendientes" => item.Estado == EstadoFacturaProveedor.Pendiente,
        "Vencidas" => item.EstaVencida,
        "Pagadas" => item.Estado == EstadoFacturaProveedor.Pagada,
        "Anuladas" => item.Estado == EstadoFacturaProveedor.Anulada,
        _ => true
    };

    protected override bool PuedeEditar(FacturaProveedor item) => _servicio.PuedeEditar(item);

    protected override bool PuedeEliminar(FacturaProveedor item) => _servicio.PuedeEliminar(item);

    protected override FacturaProveedor CrearNuevo() => new()
    {
        FechaEmision = DateTime.Today,
        FechaVencimiento = DateTime.Today.AddDays(30),
        Estado = EstadoFacturaProveedor.Pendiente,
        CreadoPorId = _sesionActual.UsuarioActual?.Id ?? 0,
        FechaCreacion = DateTime.Now
    };

    protected override CrudEditorViewModelBase<FacturaProveedor> CrearEditor(FacturaProveedor item) =>
        new FacturaProveedorEditorViewModel(item, _proveedores, _servicio);

    /// <summary>
    /// Pregunta de qué cuenta salió el dinero y lo anota en el libro de banco, además de dar la
    /// factura por pagada. Ver <see cref="CuentasPorCobrarViewModel"/> para por qué sustituyó al
    /// Confirmar de sí/no que había antes.
    /// </summary>
    private void RegistrarPago()
    {
        if (SelectedItem is not { } factura)
            return;

        var editor = new AsientoBancoEditorViewModel(
            $"Registrar pago de la factura Nº {factura.NumeroDocumento}",
            $"{factura.ProveedorNombre} — {factura.Descripcion}",
            factura.Monto,
            esEntrada: false,
            _banco.CuentasActivas(),
            "Registrar pago");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.RegistrarPago(factura, editor.Resultado,
                                              _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void Anular()
    {
        if (SelectedItem is not { } factura)
            return;

        var editor = new MotivoEditorViewModel(
            $"Anular factura {factura.NumeroDocumento}",
            $"{factura.ProveedorNombre} — {factura.Descripcion} — {factura.MontoTexto}",
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(factura, editor.Motivo));
    }

    /// <summary>
    /// La lista la repuebla la recarga que dispara la escritura del servicio; aquí solo se
    /// apunta qué factura dejar seleccionada.
    /// </summary>
    private void Aplicar(Func<FacturaProveedor> transicion)
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

/// <summary>
/// Finanzas · Cuentas por Pagar: las facturas de compra pendientes de pago. El maestro de
/// proveedores vive aparte, en Finanzas · Proveedores (<see cref="ProveedoresViewModel"/>).
/// </summary>
public sealed class CuentasPorPagarViewModel : PantallaViewModelBase
{
    public CuentasPorPagarViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private CuentasPorPagarViewModel(Modulo modulo,
                                     Submodulo submodulo,
                                     IServicioDialogo dialogos,
                                     ISesionActual sesion)
        : base(modulo, submodulo)
    {
        Facturas = new FacturasProveedorCrudViewModel(
            DataSourceFactory.CrearFacturasProveedor(), DataSourceFactory.CrearProveedores(),
            dialogos, sesion);
    }

    public FacturasProveedorCrudViewModel Facturas { get; }

    public override void Recargar() => Facturas.Recargar();
}
