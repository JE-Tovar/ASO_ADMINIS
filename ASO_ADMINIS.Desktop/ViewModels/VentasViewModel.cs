using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.Services.Fiscal;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Ventas · Registro: los documentos fiscales de venta (facturas, notas de crédito y de débito)
/// y su emisión.
///
/// Las filas no se editan, no se borran y no se anulan: un documento fiscal es inalterable y se
/// corrige con una nota. Todo pasa por <see cref="VentasService"/>, que habla con el emisor
/// fiscal y mueve el almacén.
///
/// Si el emisor no responde al emitir una factura, quien tenga permiso de contingencia puede
/// registrarla con los números de un formato de papel; queda "Por transmitir" hasta que
/// <see cref="TransmitirCommand"/> la envíe.
/// </summary>
public sealed class VentasViewModel : PantallaCrudViewModel<Venta, int>
{
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;
    private readonly InventarioService _inventario;
    private readonly VentasService _servicio;
    private readonly TasaCambioService _tasaCambio;
    private readonly IClienteDataSource _clientes;

    private string _filtro = "Todos";

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
        _clientes = DataSourceFactory.CrearClientes();

        var articulos = DataSourceFactory.CrearArticulos();
        _inventario = new InventarioService(articulos,
                                            DataSourceFactory.CrearEntradasInventario(),
                                            DataSourceFactory.CrearSalidasInventario());

        _servicio = new VentasService(ventas, articulos, DataSourceFactory.CrearSalidasInventario(),
                                      DataSourceFactory.CrearEntradasInventario(), _inventario, sesion,
                                      DataSourceFactory.CrearEmisorFiscal());

        _tasaCambio = new TasaCambioService(
            DataSourceFactory.CrearTasasCambio(), DataSourceFactory.CrearProveedorTasaCambio());

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });

        NotaCreditoCommand = new RelayCommand(() => EmitirNota(TipoDocumentoVenta.NotaCredito),
            () => _servicio.PuedeEmitirNota(SelectedItem) && _sesionActual.Puede(Permisos.Ventas.NotaCredito));

        NotaDebitoCommand = new RelayCommand(() => EmitirNota(TipoDocumentoVenta.NotaDebito),
            () => _servicio.PuedeEmitirNota(SelectedItem) && _sesionActual.Puede(Permisos.Ventas.NotaDebito));

        TransmitirCommand = new RelayCommand(Transmitir,
            () => _sesionActual.Puede(Permisos.Ventas.Contingencia) && Items.Any(v => v.Estado == EstadoVenta.PendienteTransmitir));
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand NotaCreditoCommand { get; }
    public ICommand NotaDebitoCommand { get; }
    public ICommand TransmitirCommand { get; }

    public string EmisorTexto => _servicio.Emisor.EsFiscal
        ? $"Emisor: {_servicio.Emisor.Nombre}"
        : $"Emisor: {_servicio.Emisor.Nombre} (sin valor fiscal)";

    public int Pendientes => Items.Count(v => v.Estado == EstadoVenta.PendienteTransmitir);

    public string PendientesTexto => Pendientes == 0
        ? string.Empty
        : $"{Pendientes} documento(s) de contingencia por transmitir";

    public string Resumen
    {
        get
        {
            var hoy = DateTime.Today;
            var delMes = Items.Where(v => v.Vigente && !v.EsHistorico
                                          && v.Fecha.Year == hoy.Year && v.Fecha.Month == hoy.Month);
            return $"Este mes: Bs. {delMes.Sum(v => v.Signo * v.Total):N2} " +
                   $"({delMes.Count(v => v.Tipo == TipoDocumentoVenta.Factura)} facturas)";
        }
    }

    protected override string ModuloPermiso => "Ventas";

    protected override bool CoincideBusqueda(Venta item, string texto) =>
        item.Numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.NumeroFiscal.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.NumeroControl.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.ClienteRif.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.ClienteNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Lineas.Any(l => l.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase)
                                || l.Codigo.Contains(texto, StringComparison.OrdinalIgnoreCase))
        || item.MetodoPagoTexto.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(Venta item) => _filtro switch
    {
        "Facturas" => item.Tipo == TipoDocumentoVenta.Factura && !item.EsHistorico,
        "Notas de crédito" => item.Tipo == TipoDocumentoVenta.NotaCredito,
        "Notas de débito" => item.Tipo == TipoDocumentoVenta.NotaDebito,
        "Por transmitir" => item.Estado == EstadoVenta.PendienteTransmitir,
        "Histórico" => item.EsHistorico,
        _ => true
    };

    protected override bool PuedeEditar(Venta item) => false;
    protected override bool PuedeEliminar(Venta item) => false;

    protected override Venta CrearNuevo() => new() { Tipo = TipoDocumentoVenta.Factura };

    /// <summary>
    /// Artículos, existencia y clientes se leen frescos en cada apertura del editor, no se
    /// cachean: mismo criterio que <c>SalidasViewModel.CrearEditor</c>.
    /// </summary>
    protected override CrudEditorViewModelBase<Venta> CrearEditor(Venta item) =>
        new VentaEditorViewModel(item, _inventario.ActivosConExistencia(), [.. _clientes.GetAll()],
                                 _servicio, _tasaCambio.Actual()?.Valor);

    /// <summary>Emitir factura: por el servicio, que habla con el emisor y descuenta el almacén.</summary>
    protected override void Agregar()
    {
        if (!_servicio.PuedeEmitir(out var error))
        {
            _dialogos.Informar("No se puede facturar todavía", error!);
            return;
        }

        var editor = (VentaEditorViewModel)CrearEditor(CrearNuevo());
        if (!_dialogos.MostrarEditor(editor))
            return;

        var borrador = editor.ObtenerResultado();
        var usuario = _sesionActual.UsuarioActual?.Id ?? 0;

        try
        {
            var emitida = _servicio.EmitirFactura(borrador, usuario);
            SeleccionarTrasRecargar(emitida.Id);
            Recargar();
        }
        catch (InvalidOperationException ex) when (ex.InnerException is EmisorFiscalException
                                                   && _sesionActual.Puede(Permisos.Ventas.Contingencia))
        {
            OfrecerContingencia(borrador, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudo emitir la factura", ex.Message);
        }
    }

    private void OfrecerContingencia(Venta borrador, string error)
    {
        var editor = new ContingenciaEditorViewModel(error);
        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.RegistrarEnContingencia(borrador, editor.NumeroDocumento, editor.NumeroControl,
                                                         _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void EmitirNota(TipoDocumentoVenta tipo)
    {
        if (SelectedItem is not { } factura)
            return;

        var disponible = tipo == TipoDocumentoVenta.NotaCredito ? _servicio.DisponibleParaDevolver(factura) : null;
        if (disponible is not null && disponible.Values.All(v => v <= 0))
        {
            _dialogos.Informar("Nada por devolver", "Las notas de crédito previas ya devolvieron todo lo facturado.");
            return;
        }

        var editor = new NotaFiscalEditorViewModel(factura, tipo, disponible);
        if (!_dialogos.MostrarEditor(editor))
            return;

        var usuario = _sesionActual.UsuarioActual?.Id ?? 0;
        Aplicar(() => tipo == TipoDocumentoVenta.NotaCredito
            ? _servicio.EmitirNotaCredito(factura, editor.DevolucionesElegidas, editor.Motivo, usuario)
            : _servicio.EmitirNotaDebito(factura, editor.CargosElegidos, editor.Motivo, usuario));
    }

    private void Transmitir()
    {
        try
        {
            var enviados = _servicio.TransmitirPendientes();
            Recargar();
            _dialogos.Informar("Contingencia", $"Se transmitieron {enviados} documento(s) al emisor fiscal.");
        }
        catch (EmisorFiscalException ex)
        {
            Recargar();
            _dialogos.Informar("No se pudo transmitir", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudo transmitir", ex.Message);
        }
    }

    private void Aplicar(Func<Venta> transicion)
    {
        try
        {
            SeleccionarTrasRecargar(transicion().Id);
            Recargar();
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudo completar la operación", ex.Message);
        }
    }
}
