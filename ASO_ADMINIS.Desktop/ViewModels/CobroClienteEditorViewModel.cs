using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

public sealed class CobroClienteEditorViewModel : CrudEditorViewModelBase<CobroCliente>
{
    private readonly CuentaPorCobrar _cuenta;
    private readonly CuentasPorCobrarService _servicio;

    public CobroClienteEditorViewModel(CuentaPorCobrar cuenta, CuentasPorCobrarService servicio)
    {
        _cuenta = cuenta;
        _servicio = servicio;
        Cuentas = servicio.CuentasActivas();
        CobrarTodoCommand = new RelayCommand(CobrarTodo);
    }

    public override string Titulo => $"Cobrar factura {_cuenta.NumeroFactura}";
    public override string TextoAccion => "Registrar cobro";
    public override double AnchoEditor => Ancho.Estandar;
    public string Descripcion => $"{_cuenta.ClienteTexto} · saldo Bs {_cuenta.SaldoBs:N2} (≈ US$ {_cuenta.SaldoUsdReferencia:N2} a la tasa de emisión)";
    public IReadOnlyList<PagoVentaEditorViewModel.OpcionMetodoPago> Metodos => PagoVentaEditorViewModel.Metodos;
    public IReadOnlyList<CuentaBancaria> Cuentas { get; }
    public RelayCommand CobrarTodoCommand { get; }

    private PagoVentaEditorViewModel.OpcionMetodoPago? _metodoSeleccionado;
    public PagoVentaEditorViewModel.OpcionMetodoPago? MetodoSeleccionado
    {
        get => _metodoSeleccionado;
        set
        {
            if (SetProperty(ref _metodoSeleccionado, value))
                AvisarCalculo();
        }
    }

    public bool EsDivisa => MetodoSeleccionado is { } m && Venta.EsDivisa(m.Valor);
    public bool RequiereBanco => MetodoSeleccionado?.Valor is MetodoPago.Debito or MetodoPago.Credito
        or MetodoPago.PagoMovil or MetodoPago.Transferencia or MetodoPago.Zelle;
    public string MonedaTexto => EsDivisa ? "Monto recibido (US$)" : "Monto recibido (Bs)";
    public string EquivalenteTexto => EsDivisa
        ? Tasa > 0 && MontoValor > 0
            ? $"US$ {MontoValor:N2} × Bs {Tasa:N4} = Bs {CalculoFiscal.Redondear(MontoValor * Tasa):N2}"
            : "Se convertirá a Bs con la tasa BCV de la fecha del cobro."
        : "La deuda y el abono se registran en Bs.";
    public string AvisoIgtf => EsDivisa
        ? "Si la organización es contribuyente especial, este cobro posterior en divisa quedará bloqueado hasta definir el tratamiento del IGTF."
        : string.Empty;

    private DateTime _fecha = DateTime.Today;
    public DateTime Fecha
    {
        get => _fecha;
        set
        {
            if (SetProperty(ref _fecha, value))
                AvisarCalculo();
        }
    }

    private decimal Tasa => EsDivisa ? _servicio.TasaParaCobro(Fecha) : 0m;

    private string _monto = string.Empty;
    public string Monto
    {
        get => _monto;
        set
        {
            if (SetProperty(ref _monto, value))
                AvisarCalculo();
        }
    }

    private decimal MontoValor => decimal.TryParse(Monto, out var valor) ? valor : 0m;

    private string _referencia = string.Empty;
    public string Referencia
    {
        get => _referencia;
        set => SetProperty(ref _referencia, value);
    }

    private CuentaBancaria? _cuentaSeleccionada;
    public CuentaBancaria? CuentaSeleccionada
    {
        get => _cuentaSeleccionada;
        set => SetProperty(ref _cuentaSeleccionada, value);
    }

    private void CobrarTodo()
    {
        var monto = EsDivisa
            ? Tasa > 0 ? Math.Round(_cuenta.SaldoBs / Tasa, 2, MidpointRounding.AwayFromZero) : 0m
            : CalculoFiscal.Redondear(_cuenta.SaldoBs);
        Monto = monto.ToString("0.##");
    }

    private void AvisarCalculo()
    {
        foreach (var nombre in new[] { nameof(EsDivisa), nameof(RequiereBanco), nameof(MonedaTexto),
                     nameof(EquivalenteTexto), nameof(AvisoIgtf) })
            OnPropertyChanged(nombre);
    }

    protected override bool Validar(out string? error)
    {
        if (MetodoSeleccionado is null)
        {
            error = "Seleccione el método de cobro.";
            return false;
        }
        if (MontoValor <= 0)
        {
            error = "Indique un monto mayor que cero.";
            return false;
        }
        if (Fecha.Date > DateTime.Today || Fecha.Date < _cuenta.FechaEmision.Date)
        {
            error = "La fecha debe estar entre la emisión de la factura y hoy.";
            return false;
        }
        if (RequiereBanco && CuentaSeleccionada is null)
        {
            error = "Seleccione la cuenta bancaria de destino.";
            return false;
        }
        if (!EsDivisa && MontoValor - _cuenta.SaldoBs > 0.005m)
        {
            error = $"El cobro excede el saldo pendiente de Bs {_cuenta.SaldoBs:N2}.";
            return false;
        }
        error = null;
        return true;
    }

    public override CobroCliente ObtenerResultado() => new()
    {
        Fecha = Fecha,
        Metodo = MetodoSeleccionado!.Valor,
        MontoBs = EsDivisa ? 0 : CalculoFiscal.Redondear(MontoValor),
        MontoDivisa = EsDivisa ? MontoValor : 0,
        Referencia = Referencia.Trim(),
        CuentaBancariaId = RequiereBanco ? CuentaSeleccionada?.Id : null
    };
}
