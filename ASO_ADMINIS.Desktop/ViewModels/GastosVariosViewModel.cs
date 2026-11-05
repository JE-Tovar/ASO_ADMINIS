using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Finanzas · Gastos varios: el historial de pagos que no son a proveedores (luz, alquiler,
/// fletes…) y el registro de uno nuevo.
///
/// Misma forma que Salidas: las filas son documentos de dinero, así que no se editan ni se
/// borran, se anulan — y anular uno anula también su asiento de Banco.
/// </summary>
public sealed class GastosVariosViewModel : PantallaCrudViewModel<GastoVario, int>
{
    private const string FiltroTodos = "Todos";

    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;
    private readonly GastosVariosService _servicio;

    private string _filtro = FiltroTodos;

    public GastosVariosViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearGastosVarios(),
               new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private GastosVariosViewModel(Modulo modulo,
                                  Submodulo submodulo,
                                  IGastoVarioDataSource gastos,
                                  IServicioDialogo dialogos,
                                  ISesionActual sesion)
        : base(modulo, submodulo, gastos, dialogos, sesion)
    {
        _dialogos = dialogos;
        _sesionActual = sesion;

        var banco = new BancoService(DataSourceFactory.CrearMovimientosBanco(),
                                     DataSourceFactory.CrearCuentasBancarias(), sesion);
        _servicio = new GastosVariosService(gastos, banco, DataSourceFactory.CrearTasasCambio(), sesion);

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro ?? FiltroTodos;
            ItemsView.Refresh();
            OnPropertyChanged(nameof(Resumen));
        });

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } g && _servicio.PuedeAnular(g)
                  && _sesionActual.Puede(Permisos.Finanzas.Anular));
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand AnularCommand { get; }

    /// <summary>Lo vigente del mes en curso, en Bs: la pregunta que se hace al abrir la pantalla.</summary>
    public string Resumen
    {
        get
        {
            var inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var delMes = Items.Where(g => g.Vigente && g.Fecha >= inicioMes).ToList();
            return $"{delMes.Count} gasto(s) este mes · Bs. {delMes.Sum(g => g.MontoBs):N2}";
        }
    }

    protected override string ModuloPermiso => "GastosVarios";

    protected override bool CoincideBusqueda(GastoVario item, string texto) =>
        item.Numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Categoria.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Concepto.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Beneficiario.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Referencia.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(GastoVario item) => _filtro switch
    {
        "Este mes" => item.Vigente && item.Fecha.Year == DateTime.Today.Year && item.Fecha.Month == DateTime.Today.Month,
        "En Bs" => item.Vigente && item.Moneda == MonedaGasto.Bs,
        "En US$" => item.Vigente && item.Moneda == MonedaGasto.Usd,
        "Anulados" => item.Estado == EstadoGastoVario.Anulado,
        _ => true
    };

    protected override bool PuedeEditar(GastoVario item) => false;

    protected override bool PuedeEliminar(GastoVario item) => false;

    protected override GastoVario CrearNuevo() => new()
    {
        Fecha = DateTime.Today,
        Moneda = MonedaGasto.Bs,
        Estado = EstadoGastoVario.Registrado
    };

    protected override CrudEditorViewModelBase<GastoVario> CrearEditor(GastoVario item) =>
        new GastoVarioEditorViewModel(item, _servicio);

    /// <summary>El alta pasa por el servicio: numera, congela la tasa y asienta en Banco.</summary>
    protected override void Agregar()
    {
        var editor = CrearEditor(CrearNuevo());

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Registrar(editor.ObtenerResultado(), _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void Anular()
    {
        if (SelectedItem is not { } gasto)
            return;

        var editor = new MotivoEditorViewModel(
            $"Anular gasto {gasto.Numero}",
            $"{gasto.Categoria} — {gasto.Concepto} — {gasto.MontoTexto}" +
            (gasto.CuentaId is null ? string.Empty : $"\nTambién se anulará su movimiento en {gasto.CuentaNombre}."),
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(gasto, editor.Motivo));
    }

    private void Aplicar(Func<GastoVario> transicion)
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

/// <summary>Una opción del desplegable "Se pagó con": efectivo o una cuenta de Banco.</summary>
public sealed record OpcionPagoGasto(int? CuentaId, string Texto, string MonedaCuenta);

/// <summary>Opción del desplegable de moneda, con su texto.</summary>
public sealed record OpcionMonedaGasto(MonedaGasto Valor, string Texto);

/// <summary>
/// El formulario de un gasto vario. Muestra en vivo el equivalente en Bs con la tasa que
/// tocaría en la fecha elegida; el valor definitivo lo congela el servicio al registrar.
/// </summary>
public sealed class GastoVarioEditorViewModel : CrudEditorViewModelBase<GastoVario>
{
    private readonly GastoVario _original;
    private readonly GastosVariosService _servicio;
    private readonly IReadOnlyList<CuentaBancaria> _cuentas;

    public GastoVarioEditorViewModel(GastoVario original, GastosVariosService servicio)
    {
        _original = original;
        _servicio = servicio;
        _cuentas = servicio.CuentasActivas();

        Categorias = servicio.CategoriasSugeridas();
        _fecha = original.Fecha == default ? DateTime.Today : original.Fecha;
        _monedaSeleccionada = Monedas.First(m => m.Valor == original.Moneda);
        ActualizarOpcionesDePago();
    }

    public override string Titulo => "Registrar gasto vario";
    public override double AnchoEditor => Ancho.Estandar;
    public override string TextoAccion => "Registrar gasto";

    public IReadOnlyList<string> Categorias { get; }

    public IReadOnlyList<OpcionMonedaGasto> Monedas { get; } =
    [
        new(MonedaGasto.Bs, "Bolívares (Bs)"),
        new(MonedaGasto.Usd, "Dólares (US$)")
    ];

    private IReadOnlyList<OpcionPagoGasto> _opcionesPago = [];
    public IReadOnlyList<OpcionPagoGasto> OpcionesPago
    {
        get => _opcionesPago;
        private set => SetProperty(ref _opcionesPago, value);
    }

    private OpcionPagoGasto? _pagoSeleccionado;
    public OpcionPagoGasto? PagoSeleccionado
    {
        get => _pagoSeleccionado;
        set => SetProperty(ref _pagoSeleccionado, value);
    }

    private DateTime _fecha;
    public DateTime Fecha
    {
        get => _fecha;
        set { if (SetProperty(ref _fecha, value)) OnPropertyChanged(nameof(EquivalenteTexto)); }
    }

    private string _categoria = string.Empty;
    public string Categoria
    {
        get => _categoria;
        set => SetProperty(ref _categoria, value);
    }

    private string _concepto = string.Empty;
    public string Concepto
    {
        get => _concepto;
        set => SetProperty(ref _concepto, value);
    }

    private string _beneficiario = string.Empty;
    public string Beneficiario
    {
        get => _beneficiario;
        set => SetProperty(ref _beneficiario, value);
    }

    private string _referencia = string.Empty;
    public string Referencia
    {
        get => _referencia;
        set => SetProperty(ref _referencia, value);
    }

    private OpcionMonedaGasto _monedaSeleccionada;
    public OpcionMonedaGasto MonedaSeleccionada
    {
        get => _monedaSeleccionada;
        set
        {
            if (SetProperty(ref _monedaSeleccionada, value))
            {
                ActualizarOpcionesDePago();
                OnPropertyChanged(nameof(EquivalenteTexto));
            }
        }
    }

    private string _monto = string.Empty;
    public string Monto
    {
        get => _monto;
        set { if (SetProperty(ref _monto, value)) OnPropertyChanged(nameof(EquivalenteTexto)); }
    }

    private decimal MontoValor => decimal.TryParse(Monto, out var v) ? v : 0m;

    /// <summary>"Bs. 1.234,56 (tasa 36,50)" — solo informativo hasta registrar.</summary>
    public string EquivalenteTexto
    {
        get
        {
            if (MonedaSeleccionada.Valor == MonedaGasto.Bs)
                return $"Bs. {MontoValor:N2}";

            var (tasa, bs) = _servicio.Convertir(MonedaGasto.Usd, MontoValor, Fecha);
            return tasa > 0
                ? $"Bs. {bs:N2}  (tasa BCV {tasa:N2})"
                : "Sin tasa BCV registrada: no se puede pasar a Bs.";
        }
    }

    /// <summary>
    /// Efectivo siempre; y las cuentas activas de la moneda elegida, para no ofrecer pagar
    /// dólares desde una cuenta en bolívares.
    /// </summary>
    private void ActualizarOpcionesDePago()
    {
        var anterior = PagoSeleccionado?.CuentaId;
        OpcionesPago =
        [
            new(null, "Efectivo (sin cuenta)", string.Empty),
            .. _cuentas.Where(c => GastoVario.MonedaCoincide(MonedaSeleccionada.Valor, c.Moneda))
                       .Select(c => new OpcionPagoGasto(c.Id, c.NombreConMoneda, c.Moneda))
        ];
        PagoSeleccionado = OpcionesPago.FirstOrDefault(o => o.CuentaId == anterior) ?? OpcionesPago[0];
    }

    protected override bool Validar(out string? error)
    {
        if (!decimal.TryParse(Monto, out var monto) || monto <= 0)
        {
            error = "Escriba un monto válido mayor que cero.";
            return false;
        }

        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override GastoVario ObtenerResultado()
    {
        var gasto = _original.Clonar();
        gasto.Fecha = Fecha.Date;
        gasto.Categoria = Categoria.Trim();
        gasto.Concepto = Concepto.Trim();
        gasto.Beneficiario = Beneficiario.Trim();
        gasto.Referencia = Referencia.Trim();
        gasto.Moneda = MonedaSeleccionada.Valor;
        gasto.Monto = MontoValor;
        gasto.CuentaId = PagoSeleccionado?.CuentaId;
        return gasto;
    }
}
