using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Emisión de una factura: cliente, renglones (carrito) y pagos (uno o varios), con el
/// desglose fiscal en vivo — bases por alícuota, IVA, IGTF y total.
///
/// El cálculo NO vive aquí: el editor arma el borrador y le pide a
/// <see cref="VentasService.Preparar"/> que lo calcule, que es exactamente lo que el servicio
/// repite al emitir. Así lo que se ve es lo que se emite. La validación de fondo (existencia,
/// cliente, cuadre de pagos) también es del servicio.
///
/// Los montos van en Bs a la tasa BCV vigente al abrir el editor; el precio de cada renglón se
/// escribe en US$ (es como está en el almacén) y los pagos en divisa se escriben en US$.
/// </summary>
public sealed class VentaEditorViewModel : CrudEditorViewModelBase<Venta>
{
    private readonly Venta _original;
    private readonly VentasService _servicio;
    private readonly decimal _tasa;

    public VentaEditorViewModel(Venta original,
                                IReadOnlyList<Articulo> articulos,
                                IReadOnlyList<Cliente> clientes,
                                VentasService servicio,
                                decimal? tasaCambio)
    {
        _original = original;
        _servicio = servicio;
        _tasa = tasaCambio ?? 0m;

        Articulos = articulos;
        Clientes = [ConsumidorFinal, .. clientes.Where(c => c.Activo).OrderBy(c => c.Nombre)];
        _clienteSeleccionado = ConsumidorFinal;

        Lineas.CollectionChanged += AlCambiarColeccion;
        Pagos.CollectionChanged += AlCambiarColeccion;

        AgregarLineaCommand = new RelayCommand(() => Lineas.Add(NuevaLinea()));
        QuitarLineaCommand = new RelayCommand<LineaVentaEditorViewModel>(l => { if (l is not null) Lineas.Remove(l); });
        AgregarPagoCommand = new RelayCommand(() => Pagos.Add(NuevoPago()));
        QuitarPagoCommand = new RelayCommand<PagoVentaEditorViewModel>(p => { if (p is not null) Pagos.Remove(p); });
        CobrarRestoCommand = new RelayCommand(CobrarResto);

        Lineas.Add(NuevaLinea());
        Pagos.Add(NuevoPago());
        Recalcular();
    }

    public override string Titulo => "Emitir factura";
    public override string TextoAccion => "Emitir factura";
    public override double AnchoEditor => Ancho.Amplio;

    public IReadOnlyList<Articulo> Articulos { get; }

    public string TasaTexto => _tasa > 0
        ? $"Tasa BCV: Bs. {_tasa:N4} por US$ · los precios se escriben en US$ sin IVA."
        : "Sin tasa de cambio registrada: no se puede facturar hasta actualizarla.";

    public string EmisorTexto => _servicio.Emisor.EsFiscal
        ? $"Se emitirá por: {_servicio.Emisor.Nombre}"
        : $"Se emitirá por: {_servicio.Emisor.Nombre} — SIN VALOR FISCAL";

    // ---------------------------------------------------------------- Cliente

    private static readonly Cliente ConsumidorFinal = new() { Id = 0, Nombre = "Consumidor final", Rif = string.Empty };

    public IReadOnlyList<Cliente> Clientes { get; }

    private Cliente _clienteSeleccionado;
    public Cliente ClienteSeleccionado
    {
        get => _clienteSeleccionado;
        set
        {
            if (!SetProperty(ref _clienteSeleccionado, value ?? ConsumidorFinal))
                return;

            ClienteRif = _clienteSeleccionado.Rif;
            ClienteNombre = _clienteSeleccionado.Id == 0 ? string.Empty : _clienteSeleccionado.Nombre;
            ClienteDireccion = _clienteSeleccionado.DireccionFiscal;
        }
    }

    private string _clienteRif = string.Empty;
    public string ClienteRif
    {
        get => _clienteRif;
        set
        {
            if (SetProperty(ref _clienteRif, value))
                OnPropertyChanged(nameof(ClienteRifAyuda));
        }
    }

    public string ClienteRifAyuda => string.IsNullOrWhiteSpace(ClienteRif)
        ? "Vacío = consumidor final."
        : Rif.Error(ClienteRif) ?? (Rif.EsCedula(ClienteRif) ? "✓ Cédula" : "✓ RIF válido: la dirección fiscal es obligatoria");

    private string _clienteNombre = string.Empty;
    public string ClienteNombre
    {
        get => _clienteNombre;
        set => SetProperty(ref _clienteNombre, value);
    }

    private string _clienteDireccion = string.Empty;
    public string ClienteDireccion
    {
        get => _clienteDireccion;
        set => SetProperty(ref _clienteDireccion, value);
    }

    // ---------------------------------------------------------------- Renglones y pagos

    public ObservableCollection<LineaVentaEditorViewModel> Lineas { get; } = [];
    public ObservableCollection<PagoVentaEditorViewModel> Pagos { get; } = [];

    public ICommand AgregarLineaCommand { get; }
    public ICommand QuitarLineaCommand { get; }
    public ICommand AgregarPagoCommand { get; }
    public ICommand QuitarPagoCommand { get; }
    public ICommand CobrarRestoCommand { get; }

    private LineaVentaEditorViewModel NuevaLinea()
    {
        var linea = new LineaVentaEditorViewModel(Articulos, _tasa);
        linea.Cambio += (_, _) => Recalcular();
        return linea;
    }

    private PagoVentaEditorViewModel NuevoPago()
    {
        var pago = new PagoVentaEditorViewModel(_tasa);
        pago.Cambio += (_, _) => Recalcular();
        return pago;
    }

    private void AlCambiarColeccion(object? remitente, NotifyCollectionChangedEventArgs e) => Recalcular();

    /// <summary>Pone en el último pago (o en uno nuevo) lo que falta por cobrar.</summary>
    private void CobrarResto()
    {
        if (CalculoFiscal.Pendiente(_calculado) <= 0.005m)
            return;

        var pago = Pagos.LastOrDefault(p => p.MontoValor == 0) ?? Pagos.LastOrDefault() ?? NuevoPago();
        if (!Pagos.Contains(pago))
            Pagos.Add(pago);

        if (pago.EsDivisa && _tasa > 0)
        {
            // En US$ y redondeando hacia arriba al centavo. Con IGTF el total crece al pagar en
            // divisa, así que se repite hasta cuadrar (converge en dos o tres vueltas). Cambiar
            // el monto recalcula solo, por el evento Cambio del pago.
            for (var vuelta = 0; vuelta < 5 && CalculoFiscal.Pendiente(_calculado) > 0.005m; vuelta++)
            {
                var falta = CalculoFiscal.Pendiente(_calculado);
                pago.Monto = (pago.MontoValor + Math.Ceiling(falta / _tasa * 100m) / 100m).ToString("0.##");
            }
        }
        else
        {
            pago.Monto = (pago.MontoValor + CalculoFiscal.Pendiente(_calculado)).ToString("0.##");
        }
    }

    // ---------------------------------------------------------------- Desglose en vivo

    private Venta _calculado = new();

    private void Recalcular()
    {
        _calculado = Construir();
        try
        {
            _servicio.Preparar(_calculado);
        }
        catch (InvalidOperationException)
        {
            // Sin organización activa (no debería pasar con sesión iniciada): no hay desglose.
        }

        foreach (var (vm, linea) in Lineas.Where(l => l.ArticuloSeleccionado is not null)
                     .Zip(_calculado.Lineas))
            vm.MostrarCalculo(linea);

        foreach (var nombre in new[]
                 {
                     nameof(ExentoTexto), nameof(BaseGeneralTexto), nameof(IvaGeneralTexto), nameof(BaseReducidaTexto),
                     nameof(IvaReducidaTexto), nameof(BaseAdicionalTexto), nameof(IvaAdicionalTexto), nameof(IgtfTexto),
                     nameof(TotalTexto), nameof(TotalUsdTexto), nameof(PagadoTexto), nameof(PendienteTexto),
                     nameof(HayReducida), nameof(HayAdicional), nameof(HayIgtf), nameof(HayExento)
                 })
            OnPropertyChanged(nombre);
    }

    public bool HayExento => _calculado.MontoExento != 0;
    public bool HayReducida => _calculado.BaseReducida != 0;
    public bool HayAdicional => _calculado.BaseAdicional != 0;
    public bool HayIgtf => _calculado.Igtf != 0;

    public string ExentoTexto => _calculado.MontoExento.ToString("N2");
    public string BaseGeneralTexto => $"{_calculado.BaseGeneral:N2}";
    public string IvaGeneralTexto => $"IVA {_calculado.PorcentajeGeneral:0.##} %: {_calculado.IvaGeneral:N2}";
    public string BaseReducidaTexto => $"{_calculado.BaseReducida:N2}";
    public string IvaReducidaTexto => $"IVA {_calculado.PorcentajeReducida:0.##} %: {_calculado.IvaReducida:N2}";
    public string BaseAdicionalTexto => $"{_calculado.BaseAdicional:N2}";
    public string IvaAdicionalTexto => $"IVA {_calculado.PorcentajeAdicional:0.##} %: {_calculado.IvaAdicional:N2}";
    public string IgtfTexto => $"IGTF {_calculado.PorcentajeIgtf:0.##} % s/ {_calculado.BaseIgtf:N2}: {_calculado.Igtf:N2}";
    public string TotalTexto => $"Bs. {_calculado.Total:N2}";
    public string TotalUsdTexto => _tasa > 0 ? $"≈ US$ {_calculado.Total / _tasa:N2}" : string.Empty;
    public string PagadoTexto => $"Bs. {_calculado.Pagos.Sum(p => p.MontoBs):N2}";

    public string PendienteTexto
    {
        get
        {
            var pendiente = CalculoFiscal.Pendiente(_calculado);
            return pendiente switch
            {
                > 0.005m => $"Falta: Bs. {pendiente:N2}",
                < -0.005m => $"Vuelto: Bs. {-pendiente:N2}",
                _ => "Cobro completo"
            };
        }
    }

    // ---------------------------------------------------------------- Resultado

    private Venta Construir()
    {
        var venta = _original.Clonar();
        venta.Tipo = TipoDocumentoVenta.Factura;
        venta.TasaCambioUsada = _tasa;
        venta.ClienteId = ClienteSeleccionado.Id == 0 ? null : ClienteSeleccionado.Id;
        venta.ClienteRif = ClienteRif.Trim();
        venta.ClienteNombre = ClienteNombre.Trim();
        venta.ClienteDireccion = ClienteDireccion.Trim();
        venta.Lineas = [.. Lineas.Where(l => l.ArticuloSeleccionado is not null).Select(l => l.Construir())];
        venta.Pagos = [.. Pagos.Where(p => p.MetodoSeleccionado is not null && p.MontoValor > 0).Select(p => p.Construir())];
        return venta;
    }

    protected override bool Validar(out string? error)
    {
        if (Lineas.Any(l => l.ArticuloSeleccionado is not null && (!l.CantidadEsValida || !l.PrecioEsValido)))
        {
            error = "Hay una cantidad o un precio que no es un número válido.";
            return false;
        }

        if (Pagos.Any(p => !string.IsNullOrWhiteSpace(p.Monto) && (p.MetodoSeleccionado is null || p.MontoValor <= 0)))
        {
            error = "Cada pago necesita su método y un monto mayor que cero.";
            return false;
        }

        var factura = ObtenerResultado();
        return _servicio.Validar(factura, out error);
    }

    public override Venta ObtenerResultado()
    {
        var venta = Construir();
        _servicio.Preparar(venta);
        return venta;
    }
}

/// <summary>Un renglón del carrito mientras se escribe.</summary>
public sealed class LineaVentaEditorViewModel : ViewModelBase
{
    private readonly decimal _tasa;

    public event EventHandler? Cambio;

    public LineaVentaEditorViewModel(IReadOnlyList<Articulo> articulos, decimal tasa)
    {
        Articulos = articulos;
        _tasa = tasa;
    }

    public IReadOnlyList<Articulo> Articulos { get; }

    private Articulo? _articuloSeleccionado;
    public Articulo? ArticuloSeleccionado
    {
        get => _articuloSeleccionado;
        set
        {
            if (!SetProperty(ref _articuloSeleccionado, value))
                return;

            Presentaciones = value?.OpcionesDePresentacion() ?? [];
            OnPropertyChanged(nameof(Presentaciones));
            OnPropertyChanged(nameof(AlicuotaTexto));
            PresentacionSeleccionada = Presentaciones.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(Cantidad) && value is not null)
                Cantidad = "1";
            Avisar();
        }
    }

    public IReadOnlyList<OpcionPresentacion> Presentaciones { get; private set; } = [];

    private OpcionPresentacion? _presentacionSeleccionada;
    public OpcionPresentacion? PresentacionSeleccionada
    {
        get => _presentacionSeleccionada;
        set
        {
            if (!SetProperty(ref _presentacionSeleccionada, value))
                return;

            // El precio se propone del artículo, pero sigue editable (oferta, ajuste).
            if (value is not null)
                PrecioUnitario = value.PrecioVenta == 0 ? string.Empty : value.PrecioVenta.ToString("0.##");
            Avisar();
        }
    }

    private decimal Factor => PresentacionSeleccionada?.Factor ?? 1m;

    private string _cantidad = string.Empty;
    public string Cantidad
    {
        get => _cantidad;
        set { if (SetProperty(ref _cantidad, value)) Avisar(); }
    }

    private string _precioUnitario = string.Empty;
    public string PrecioUnitario
    {
        get => _precioUnitario;
        set { if (SetProperty(ref _precioUnitario, value)) Avisar(); }
    }

    public bool CantidadEsValida => decimal.TryParse(Cantidad, out var v) && v > 0;
    public bool PrecioEsValido => decimal.TryParse(PrecioUnitario, out var v) && v >= 0;
    private decimal CantidadValor => decimal.TryParse(Cantidad, out var v) ? v : 0m;
    private decimal PrecioValor => decimal.TryParse(PrecioUnitario, out var v) ? v : 0m;

    public string AlicuotaTexto => ArticuloSeleccionado is { } a ? Alicuotas.Marca(a.Alicuota) : string.Empty;

    public string ExistenciaTexto => ArticuloSeleccionado is { } a ? $"{a.Existencia:N2} {a.UnidadCorta}" : string.Empty;

    /// <summary>Se pinta en rojo si se pide más de lo disponible; el servicio lo vuelve a comprobar.</summary>
    public bool SePasa => ArticuloSeleccionado is { } a && CantidadValor * Factor > a.Existencia;

    private string _montoTexto = string.Empty;
    public string MontoTexto
    {
        get => _montoTexto;
        private set => SetProperty(ref _montoTexto, value);
    }

    /// <summary>El editor le pasa el renglón ya calculado por el servicio.</summary>
    public void MostrarCalculo(VentaLinea calculada) => MontoTexto = calculada.Monto.ToString("N2");

    public VentaLinea Construir() => new()
    {
        ArticuloId = ArticuloSeleccionado!.Id,
        Codigo = ArticuloSeleccionado.Codigo,
        Descripcion = ArticuloSeleccionado.Nombre,
        PresentacionNombre = PresentacionSeleccionada?.Nombre ?? ArticuloSeleccionado.UnidadCorta,
        Factor = Factor,
        Cantidad = CantidadValor,
        PrecioUnitarioUsd = PrecioValor,
        Alicuota = ArticuloSeleccionado.Alicuota
    };

    private void Avisar()
    {
        OnPropertyChanged(nameof(ExistenciaTexto));
        OnPropertyChanged(nameof(SePasa));
        if (ArticuloSeleccionado is null)
            MontoTexto = string.Empty;
        Cambio?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Un pago mientras se escribe. En divisa, el monto va en US$.</summary>
public sealed class PagoVentaEditorViewModel : ViewModelBase
{
    private readonly decimal _tasa;

    public event EventHandler? Cambio;

    public PagoVentaEditorViewModel(decimal tasa) => _tasa = tasa;

    public sealed record OpcionMetodoPago(MetodoPago Valor, string Texto);

    public static IReadOnlyList<OpcionMetodoPago> Metodos { get; } =
        [.. Enum.GetValues<MetodoPago>().Select(m => new OpcionMetodoPago(m, Venta.TextoDe(m)))];

    public IReadOnlyList<OpcionMetodoPago> MetodosPago => Metodos;

    // Sin preselección a propósito: un método por defecto se quedaría puesto sin que nadie lo
    // mirara, y el cuadre de caja por método saldría mal.
    private OpcionMetodoPago? _metodoSeleccionado;
    public OpcionMetodoPago? MetodoSeleccionado
    {
        get => _metodoSeleccionado;
        set
        {
            if (!SetProperty(ref _metodoSeleccionado, value))
                return;
            OnPropertyChanged(nameof(EsDivisa));
            OnPropertyChanged(nameof(MonedaTexto));
            Avisar();
        }
    }

    public bool EsDivisa => MetodoSeleccionado is { } m && Venta.EsDivisa(m.Valor);
    public string MonedaTexto => EsDivisa ? "US$" : "Bs";

    private string _monto = string.Empty;
    public string Monto
    {
        get => _monto;
        set { if (SetProperty(ref _monto, value)) Avisar(); }
    }

    private string _referencia = string.Empty;
    public string Referencia
    {
        get => _referencia;
        set => SetProperty(ref _referencia, value);
    }

    public decimal MontoValor => decimal.TryParse(Monto, out var v) ? v : 0m;

    public string EquivalenteTexto => EsDivisa && _tasa > 0 && MontoValor > 0
        ? $"= Bs. {MontoValor * _tasa:N2}"
        : string.Empty;

    public VentaPago Construir() => new()
    {
        Metodo = MetodoSeleccionado!.Valor,
        MontoDivisa = EsDivisa ? MontoValor : 0m,
        MontoBs = EsDivisa ? CalculoFiscal.Redondear(MontoValor * _tasa) : MontoValor,
        Referencia = Referencia.Trim()
    };

    private void Avisar()
    {
        OnPropertyChanged(nameof(EquivalenteTexto));
        Cambio?.Invoke(this, EventArgs.Empty);
    }
}
