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
/// Los montos van en Bs a la tasa BCV vigente al abrir el editor. El precio de cada renglón NO
/// se escribe: es el del artículo o servicio en su catálogo (en US$), y el servicio lo vuelve a
/// fijar desde la base al emitir. Los pagos en divisa se escriben en US$.
/// </summary>
public sealed class VentaEditorViewModel : CrudEditorViewModelBase<Venta>
{
    private readonly Venta _original;
    private readonly VentasService _servicio;
    private readonly decimal _tasa;

    public VentaEditorViewModel(Venta original,
                                IReadOnlyList<Articulo> articulos,
                                ClientesService clientes,
                                VentasService servicio,
                                decimal? tasaCambio,
                                FormatoEtiqueta? etiquetas = null)
    {
        _original = original;
        _servicio = servicio;
        _tasa = tasaCambio ?? 0m;

        Articulos = articulos;
        _clientes = clientes;

        ElegirClienteCommand = new RelayCommand<Cliente>(c => { if (c is not null) UsarCliente(c); });
        ConfirmarClienteCommand = new RelayCommand(ConfirmarCliente);
        BajarClienteCommand = new RelayCommand(() => MoverSugerencia(+1));
        SubirClienteCommand = new RelayCommand(() => MoverSugerencia(-1));
        CerrarSugerenciasCommand = new RelayCommand(() => MostrarSugerencias = false);
        LimpiarClienteCommand = new RelayCommand(LimpiarCliente);

        Lineas.CollectionChanged += AlCambiarColeccion;
        Pagos.CollectionChanged += AlCambiarColeccion;

        Buscador = new BuscadorArticulosViewModel(articulos, AgregarArticulo, etiquetas);
        QuitarLineaCommand = new RelayCommand<LineaVentaEditorViewModel>(l => { if (l is not null) Lineas.Remove(l); });
        AgregarPagoCommand = new RelayCommand(() => Pagos.Add(NuevoPago()));
        QuitarPagoCommand = new RelayCommand<PagoVentaEditorViewModel>(p => { if (p is not null) Pagos.Remove(p); });
        CobrarRestoCommand = new RelayCommand(CobrarResto);

        Pagos.Add(NuevoPago());
        Recalcular();
    }

    public override string Titulo => "Emitir factura";
    public override string TextoAccion => "Emitir factura";
    public override double AnchoEditor => Ancho.Amplio;

    /// <summary>Trabajo de mostrador: abre maximizada, con los renglones a todo el alto.</summary>
    public override bool AbreMaximizado => true;

    public IReadOnlyList<Articulo> Articulos { get; }

    public string TasaTexto => _tasa > 0
        ? $"Tasa BCV: Bs. {_tasa:N4} por US$ · los precios son los del catálogo, en US$ sin IVA."
        : "Sin tasa de cambio registrada: no se puede facturar hasta actualizarla.";

    public string EmisorTexto => _servicio.Emisor.EsFiscal
        ? $"Se emitirá por: {_servicio.Emisor.Nombre}"
        : $"Se emitirá por: {_servicio.Emisor.Nombre} — SIN VALOR FISCAL";

    // ---------------------------------------------------------------- Cliente
    //
    // Los cinco datos se escriben aquí. El campo RIF/C.I. sugiere clientes de la lista mientras se
    // escribe (por identificación o por nombre) y autocompleta si lo escrito es exactamente la
    // identificación de uno. Al emitir, VentasService da de alta al cliente nuevo o pone al día
    // su ficha (ClientesService.RegistrarDesdeFactura).

    private readonly ClientesService _clientes;

    /// <summary>El cliente de la lista cuyos datos se cargaron (null = nuevo o sin escribir).</summary>
    private Cliente? _clienteCargado;

    public ICommand ElegirClienteCommand { get; }
    public ICommand ConfirmarClienteCommand { get; }
    public ICommand BajarClienteCommand { get; }
    public ICommand SubirClienteCommand { get; }
    public ICommand CerrarSugerenciasCommand { get; }
    public ICommand LimpiarClienteCommand { get; }

    public ObservableCollection<Cliente> Sugerencias { get; } = [];

    private Cliente? _sugerenciaSeleccionada;
    public Cliente? SugerenciaSeleccionada
    {
        get => _sugerenciaSeleccionada;
        set => SetProperty(ref _sugerenciaSeleccionada, value);
    }

    private bool _mostrarSugerencias;
    public bool MostrarSugerencias
    {
        get => _mostrarSugerencias;
        set => SetProperty(ref _mostrarSugerencias, value);
    }

    private bool _cargando;

    private string _clienteRif = string.Empty;
    public string ClienteRif
    {
        get => _clienteRif;
        set
        {
            if (!SetProperty(ref _clienteRif, value ?? string.Empty))
                return;
            OnPropertyChanged(nameof(ClienteRifAyuda));
            if (_cargando)
                return;

            // Si cambió la identificación, el cliente cargado ya no es el de esta factura. Lo que se
            // había autocompletado y nadie tocó se borra: al teclear una cédula, "V-1234567" puede
            // ser otra persona a medio camino de "V-12345678", y sus datos no deben quedarse.
            if (_clienteCargado is { } anterior && !ClientesService.MismaIdentificacion(anterior.Rif, _clienteRif))
            {
                _clienteCargado = null;
                _cargando = true;
                try
                {
                    if (ClienteNombre == anterior.Nombre) ClienteNombre = string.Empty;
                    if (ClienteDireccion == anterior.DireccionFiscal) ClienteDireccion = string.Empty;
                    if (ClienteTelefono == anterior.Telefono) ClienteTelefono = string.Empty;
                    if (ClienteCorreo == anterior.Correo) ClienteCorreo = string.Empty;
                }
                finally
                {
                    _cargando = false;
                }
            }

            // Identificación exacta de alguien de la lista: se carga en el acto.
            if (_clienteCargado is null && _clientes.PorIdentificacion(_clienteRif) is { } exacto)
            {
                UsarCliente(exacto);
                return;
            }

            ActualizarSugerencias();
            AvisarEstadoCliente();
        }
    }

    public string ClienteRifAyuda => string.IsNullOrWhiteSpace(ClienteRif)
        ? "Obligatorio. Escriba la cédula, el RIF o el nombre para buscarlo."
        : Rif.Normalizar(ClienteRif) is null
            ? "Buscando por nombre: elija de la lista o escriba la identificación."
            : Rif.Error(ClienteRif) ?? (Rif.EsCedula(ClienteRif) ? "✓ Cédula" : "✓ RIF válido");

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

    private string _clienteTelefono = string.Empty;
    public string ClienteTelefono
    {
        get => _clienteTelefono;
        set => SetProperty(ref _clienteTelefono, value);
    }

    private string _clienteCorreo = string.Empty;
    public string ClienteCorreo
    {
        get => _clienteCorreo;
        set
        {
            if (SetProperty(ref _clienteCorreo, value))
                OnPropertyChanged(nameof(ClienteCorreoAyuda));
        }
    }

    public string ClienteCorreoAyuda => ClientesService.CorreoValido(ClienteCorreo)
        ? "Opcional."
        : "No parece un correo (nombre@dominio.com).";

    public bool EsConsumidorFinal => string.IsNullOrWhiteSpace(ClienteRif);
    public bool EsClienteRegistrado => _clienteCargado is not null;
    public bool EsClienteNuevo => !EsConsumidorFinal && _clienteCargado is null && Rif.Normalizar(ClienteRif) is not null;

    public string EstadoClienteTexto => EsConsumidorFinal
        ? "Faltan los datos del cliente"
        : EsClienteRegistrado
            ? "Cliente registrado"
            : EsClienteNuevo
                ? "Cliente nuevo: se agregará a la lista al emitir"
                : "Identificación incompleta";

    private void AvisarEstadoCliente()
    {
        OnPropertyChanged(nameof(EsConsumidorFinal));
        OnPropertyChanged(nameof(EsClienteRegistrado));
        OnPropertyChanged(nameof(EsClienteNuevo));
        OnPropertyChanged(nameof(EstadoClienteTexto));
    }

    /// <summary>Rellena los cinco campos con la ficha del cliente (y normaliza su RIF a la vista).</summary>
    public void UsarCliente(Cliente cliente)
    {
        _cargando = true;
        try
        {
            _clienteCargado = cliente;
            ClienteRif = cliente.Rif;
            ClienteNombre = cliente.Nombre;
            ClienteDireccion = cliente.DireccionFiscal;
            ClienteTelefono = cliente.Telefono;
            ClienteCorreo = cliente.Correo;
        }
        finally
        {
            _cargando = false;
        }

        Sugerencias.Clear();
        MostrarSugerencias = false;
        AvisarEstadoCliente();
    }

    private void LimpiarCliente()
    {
        _cargando = true;
        try
        {
            _clienteCargado = null;
            ClienteRif = string.Empty;
            ClienteNombre = string.Empty;
            ClienteDireccion = string.Empty;
            ClienteTelefono = string.Empty;
            ClienteCorreo = string.Empty;
        }
        finally
        {
            _cargando = false;
        }

        Sugerencias.Clear();
        MostrarSugerencias = false;
        AvisarEstadoCliente();
    }

    private void ActualizarSugerencias()
    {
        Sugerencias.Clear();
        foreach (var c in _clientes.Sugerencias(ClienteRif))
            Sugerencias.Add(c);
        SugerenciaSeleccionada = Sugerencias.FirstOrDefault();
        MostrarSugerencias = Sugerencias.Count > 0;
    }

    /// <summary>Enter en el campo RIF/C.I.: elige la sugerencia resaltada, o normaliza lo escrito.</summary>
    public void ConfirmarCliente()
    {
        if (MostrarSugerencias && SugerenciaSeleccionada is { } elegido)
        {
            UsarCliente(elegido);
            return;
        }

        MostrarSugerencias = false;
        if (Rif.Normalizar(ClienteRif) is { } normal && normal != ClienteRif)
            ClienteRif = normal;
    }

    private void MoverSugerencia(int paso)
    {
        if (Sugerencias.Count == 0)
            return;
        if (!MostrarSugerencias)
        {
            MostrarSugerencias = true;
            return;
        }

        var i = SugerenciaSeleccionada is null ? -1 : Sugerencias.IndexOf(SugerenciaSeleccionada);
        SugerenciaSeleccionada = Sugerencias[Math.Clamp(i + paso, 0, Sugerencias.Count - 1)];
    }

    // ---------------------------------------------------------------- Renglones y pagos

    public ObservableCollection<LineaVentaEditorViewModel> Lineas { get; } = [];
    public ObservableCollection<PagoVentaEditorViewModel> Pagos { get; } = [];

    /// <summary>Barra de búsqueda de los renglones (código, descripción o categoría).</summary>
    public BuscadorArticulosViewModel Buscador { get; }

    public ICommand QuitarLineaCommand { get; }
    public ICommand AgregarPagoCommand { get; }
    public ICommand QuitarPagoCommand { get; }
    public ICommand CobrarRestoCommand { get; }

    /// <summary>
    /// Lo que hace el buscador al elegir.
    /// <list type="bullet">
    /// <item>Con cantidad (escrita como "1,25*código" o leída de la etiqueta de la balanza): se
    /// suma al renglón del artículo en su unidad base, o se abre uno con esa cantidad.</item>
    /// <item>Sin cantidad y a granel: siempre un renglón nuevo con la cantidad vacía, y el cursor
    /// salta a ella (<see cref="Controls.Enfoque"/>): lo que se pesa no viene de a 1.</item>
    /// <item>Sin cantidad y por unidad: suma 1 (cómodo con lector de código de barras).</item>
    /// </list>
    /// </summary>
    public void AgregarArticulo(Articulo articulo, decimal? cantidad = null)
    {
        if (cantidad is null && articulo.EsGranel)
        {
            var pesar = NuevaLinea();
            pesar.ArticuloSeleccionado = articulo;
            pesar.Cantidad = string.Empty;
            pesar.PideCantidad = true;
            Lineas.Add(pesar);
            return;
        }

        var suma = cantidad ?? 1m;
        var existente = Lineas.FirstOrDefault(l => l.ArticuloSeleccionado?.Id == articulo.Id);
        if (existente is not null)
        {
            var actual = decimal.TryParse(existente.Cantidad, out var c) ? c : 0m;
            existente.Cantidad = (actual + suma).ToString("0.###");
            return;
        }

        var linea = NuevaLinea();
        linea.ArticuloSeleccionado = articulo;
        linea.Cantidad = suma.ToString("0.###");
        Lineas.Add(linea);
    }

    public bool SinLineas => Lineas.Count == 0;

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
                     nameof(HayReducida), nameof(HayAdicional), nameof(HayIgtf), nameof(HayExento),
                     nameof(FaltaCobrar), nameof(HayVuelto), nameof(CobroCompleto), nameof(SinLineas)
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

    /// <summary>Para el color del chip de cobro: falta (ámbar), vuelto (azul) o completo (verde).</summary>
    public bool FaltaCobrar => CalculoFiscal.Pendiente(_calculado) > 0.005m;
    public bool HayVuelto => CalculoFiscal.Pendiente(_calculado) < -0.005m;
    public bool CobroCompleto => !FaltaCobrar && !HayVuelto && _calculado.Total > 0;

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
        // El servicio lo confirma (o lo da de alta) al emitir.
        venta.ClienteId = _clienteCargado?.Id;
        venta.ClienteRif = (Rif.Normalizar(ClienteRif) ?? ClienteRif).Trim();
        venta.ClienteNombre = ClienteNombre.Trim();
        venta.ClienteDireccion = ClienteDireccion.Trim();
        venta.ClienteTelefono = ClienteTelefono.Trim();
        venta.ClienteCorreo = ClienteCorreo.Trim();
        venta.Lineas = [.. Lineas.Where(l => l.ArticuloSeleccionado is not null).Select(l => l.Construir())];
        venta.Pagos = [.. Pagos.Where(p => p.MetodoSeleccionado is not null && p.MontoValor > 0).Select(p => p.Construir())];
        return venta;
    }

    protected override bool Validar(out string? error)
    {
        if (Lineas.Any(l => l.ArticuloSeleccionado is not null && !l.CantidadEsValida))
        {
            error = "Hay una cantidad que no es un número válido.";
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

            OnPropertyChanged(nameof(AlicuotaTexto));
            OnPropertyChanged(nameof(UnidadTexto));
            // El precio es el del catálogo y no se edita aquí: quien factura no lo cambia.
            OnPropertyChanged(nameof(PrecioUnitario));
            if (string.IsNullOrWhiteSpace(Cantidad) && value is not null)
                Cantidad = "1";
            Avisar();
        }
    }

    /// <summary>Unidad base del artículo ("pza", "kg", "serv"): se vende solo en ella.</summary>
    public string UnidadTexto => ArticuloSeleccionado?.UnidadCorta ?? string.Empty;

    private string _cantidad = string.Empty;
    public string Cantidad
    {
        get => _cantidad;
        set { if (SetProperty(ref _cantidad, value)) Avisar(); }
    }

    private bool _pideCantidad;
    /// <summary>
    /// Renglón a granel recién agregado sin cantidad: el cursor salta a su celda de cantidad
    /// (Controls/Enfoque.AlCargar). Se apaga al escribirla.
    /// </summary>
    public bool PideCantidad
    {
        get => _pideCantidad;
        set => SetProperty(ref _pideCantidad, value);
    }

    /// <summary>Precio US$ sin IVA del catálogo, de solo lectura.</summary>
    public string PrecioUnitario => ArticuloSeleccionado is null
        ? string.Empty
        : PrecioValor > 0 ? PrecioValor.ToString("N2") : "Sin precio";

    public bool CantidadEsValida => decimal.TryParse(Cantidad, out var v) && v > 0;
    private decimal CantidadValor => decimal.TryParse(Cantidad, out var v) ? v : 0m;
    private decimal PrecioValor => ArticuloSeleccionado?.PrecioVenta ?? 0m;

    public string AlicuotaTexto => ArticuloSeleccionado is { } a ? Alicuotas.Marca(a.Alicuota) : string.Empty;

    public string ExistenciaTexto => ArticuloSeleccionado switch
    {
        null => string.Empty,
        { EsServicio: true } => "Servicio",
        var a => $"{a.Existencia:N2} {a.UnidadCorta}"
    };

    /// <summary>Se pinta en rojo si se pide más de lo disponible; el servicio lo vuelve a comprobar.</summary>
    public bool SePasa => ArticuloSeleccionado is { EsServicio: false } a && CantidadValor > a.Existencia;

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
        EsServicio = ArticuloSeleccionado.EsServicio,
        Codigo = ArticuloSeleccionado.Codigo,
        Descripcion = ArticuloSeleccionado.Descripcion,
        PresentacionNombre = ArticuloSeleccionado.UnidadCorta,
        Factor = 1m,
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

    /// <summary>"US$ 40,93 = Bs. 1.637,20" en divisa; "Bs." en bolívares, para que se sepa en qué se escribe.</summary>
    public string EquivalenteTexto => MetodoSeleccionado is null
        ? string.Empty
        : EsDivisa
            ? (_tasa > 0 && MontoValor > 0 ? $"US$ → Bs. {MontoValor * _tasa:N2}" : "en US$")
            : "en Bs.";

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
