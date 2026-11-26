using System.Windows.Input;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

public sealed class CuentaPorCobrarDetalleViewModel : DetalleViewModelBase<CuentaPorCobrar>
{
    private readonly CuentasPorCobrarService _servicio;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesion;

    public CuentaPorCobrarDetalleViewModel(CuentaPorCobrar cuenta, CuentasPorCobrarService servicio,
                                           IServicioDialogo dialogos, ISesionActual sesion) : base(cuenta)
    {
        _servicio = servicio;
        _dialogos = dialogos;
        _sesion = sesion;
        Movimientos = servicio.Movimientos(cuenta.Id);
        Recibos = servicio.Cobros(cuenta.Id);
        AnularCobroCommand = new RelayCommand(AnularCobro,
            () => ReciboSeleccionado is { Vigente: true } && sesion.Puede(Permisos.CuentasPorCobrar.AnularCobro));
        ImprimirReciboCommand = new RelayCommand(ImprimirRecibo, () => ReciboSeleccionado is not null);
    }

    public override string Titulo => $"Estado de cuenta · factura {Documento.NumeroFactura}";
    public override double AnchoEditor => Ancho.Amplio;
    public IReadOnlyList<MovimientoCuentaPorCobrar> Movimientos { get; }
    public IReadOnlyList<CobroCliente> Recibos { get; }
    public bool TieneRecibos => Recibos.Count > 0;
    public bool NoTieneRecibos => !TieneRecibos;
    public ICommand AnularCobroCommand { get; }
    public ICommand ImprimirReciboCommand { get; }
    public string PagoInicialTexto => Documento.PagoInicialBs.ToString("N2");
    public string TasaTexto => $"Bs {Documento.TasaCambioEmision:N4} por US$ (tasa de emisión)";

    private CobroCliente? _reciboSeleccionado;
    public CobroCliente? ReciboSeleccionado
    {
        get => _reciboSeleccionado;
        set
        {
            if (SetProperty(ref _reciboSeleccionado, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    private void AnularCobro()
    {
        if (ReciboSeleccionado is not { } recibo)
            return;
        var editor = new MotivoEditorViewModel($"Anular recibo {recibo.Numero}",
            $"Factura {Documento.NumeroFactura} · Bs {recibo.MontoBs:N2}",
            "Motivo de la anulación", "Indique el motivo de la anulación.");
        if (!_dialogos.MostrarEditor(editor))
            return;
        try
        {
            _servicio.AnularCobro(recibo, editor.Motivo, _sesion.UsuarioActual?.Id ?? 0);
            Cerrar(true);
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudo anular el cobro", ex.Message);
        }
    }

    private void ImprimirRecibo()
    {
        if (ReciboSeleccionado is not { } recibo)
            return;
        try
        {
            var organizacion = Ambito.Actual ?? throw new InvalidOperationException("No hay una organización activa.");
            var comprobante = new ComprobanteNoFiscal(
                organizacion.RazonSocial, organizacion.Rif, organizacion.DireccionFiscal, organizacion.Telefono,
                $"Recibo de cobro {recibo.Numero}", ComprobanteNoFiscal.TextoBanda,
                [("Fecha", recibo.FechaTexto), ("Factura", Documento.NumeroFactura),
                 ("Método", recibo.MetodoTexto), ("Referencia", string.IsNullOrWhiteSpace(recibo.Referencia) ? "—" : recibo.Referencia),
                 ("Estado", recibo.EstadoTexto)],
                [("RIF / C.I.", Documento.ClienteRif), ("Nombre", Documento.ClienteNombre)],
                [new ComprobanteNoFiscal.Renglon("1", $"Abono a la factura {Documento.NumeroFactura}",
                    recibo.MontoBs.ToString("N2"), "—", recibo.MontoBs.ToString("N2"))],
                [], recibo.MontoBs.ToString("N2"), recibo.EsDivisa ? $"US$ {recibo.MontoDivisa:N2}" : string.Empty,
                [(recibo.MetodoTexto, $"Bs {recibo.MontoBs:N2}", recibo.Referencia)],
                ["Recibo administrativo de cobranza; no sustituye la factura fiscal.",
                 recibo.Vigente ? "Gracias por su pago." : $"ANULADO: {recibo.MotivoAnulacion}"],
                $"Recibo-{recibo.Numero}.pdf");
            ComprobanteImpreso.Imprimir(comprobante);
        }
        catch (Exception ex)
        {
            _dialogos.Informar("No se pudo imprimir el recibo", ex.Message);
        }
    }
}
