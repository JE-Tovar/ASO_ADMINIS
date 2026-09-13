using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// El extracto de una cuenta: lo que entró, lo que salió y cómo quedó el saldo. Sub-listado de
/// Finanzas · Banco; el encabezado y el conmutador los pone <see cref="BancoViewModel"/>.
///
/// La mayoría de las filas no se teclean aquí: bajan solas desde la factura que se cobró, la que
/// se pagó y la liquidación que se pagó. Lo que sí se registra a mano es lo que no tiene
/// documento — la comisión del banco, un retiro, un aporte.
/// </summary>
public sealed class MovimientosBancoCrudViewModel : CrudViewModelBase<MovimientoBanco, int>
{
    private const string FiltroTodos = "Todos";

    private readonly ICuentaBancariaDataSource _cuentas;
    private readonly BancoService _servicio;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;

    private string _filtroEstado = FiltroTodos;

    public MovimientosBancoCrudViewModel(IMovimientoBancoDataSource movimientos,
                                         ICuentaBancariaDataSource cuentas,
                                         BancoService servicio,
                                         IServicioDialogo dialogos,
                                         ISesionActual sesion)
        : base(movimientos, dialogos, sesion)
    {
        _cuentas = cuentas;
        _servicio = servicio;
        _dialogos = dialogos;
        _sesionActual = sesion;

        Cuentas = new ObservableCollection<CuentaBancaria>(cuentas.GetAll());
        _cuentaSeleccionada = Cuentas.FirstOrDefault(c => c.Activa) ?? Cuentas.FirstOrDefault();

        CambiarFiltroEstadoCommand = new RelayCommand<string>(filtro =>
        {
            _filtroEstado = filtro;
            ItemsView.Refresh();
        });

        ConciliarCommand = new RelayCommand(Conciliar,
            () => SelectedItem is { } m && _servicio.PuedeConciliar(m) && _sesionActual.Puede(Permisos.Banco.Conciliar));

        DesconciliarCommand = new RelayCommand(Desconciliar,
            () => SelectedItem is { } m && _servicio.PuedeDesconciliar(m) && _sesionActual.Puede(Permisos.Banco.Conciliar));

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } m && _servicio.PuedeAnular(m) && _sesionActual.Puede(Permisos.Banco.Anular));

        TransferirCommand = new RelayCommand(Transferir,
            () => _sesionActual.Puede(Permisos.Banco.Transferir));

        CalcularSaldoCorrido();
    }

    public ICommand CambiarFiltroEstadoCommand { get; }
    public ICommand ConciliarCommand { get; }
    public ICommand DesconciliarCommand { get; }
    public ICommand AnularCommand { get; }
    public ICommand TransferirCommand { get; }

    /// <summary>
    /// Todas las cuentas, incluidas las cerradas: sus movimientos viejos siguen ahí y hay que
    /// poder consultarlos. Elegir cuenta al REGISTRAR sí se limita a las activas.
    /// </summary>
    public ObservableCollection<CuentaBancaria> Cuentas { get; }

    /// <summary>
    /// La cuenta que se está mirando. Va en la pantalla y no en el diálogo, con el mismo criterio
    /// que el frente de <see cref="HorariosViewModel"/>: se trabaja una cuenta seguida, así que
    /// preguntarla en cada alta sería teclear diez veces la misma respuesta. Además acota la
    /// tabla, que es lo que hace que el saldo de la cabecera signifique algo.
    /// </summary>
    private CuentaBancaria? _cuentaSeleccionada;
    public CuentaBancaria? CuentaSeleccionada
    {
        get => _cuentaSeleccionada;
        set
        {
            if (!SetProperty(ref _cuentaSeleccionada, value))
                return;

            CalcularSaldoCorrido();
            ItemsView.Refresh();
            OnTodasLasPropiedadesCambiaron();
        }
    }

    public bool HayCuentas => Cuentas.Count > 0;

    /// <summary>
    /// Acota el extracto a un rango de fechas. Nulo quiere decir "sin límite por ese lado", que es
    /// como arranca: el libro completo. No toca el saldo corrido — ese se calcula sobre toda la
    /// historia de la cuenta, no sobre lo que se ve (ver <see cref="CalcularSaldoCorrido"/>).
    /// </summary>
    private DateTime? _desde;
    public DateTime? Desde
    {
        get => _desde;
        set { if (SetProperty(ref _desde, value)) ItemsView.Refresh(); }
    }

    private DateTime? _hasta;
    public DateTime? Hasta
    {
        get => _hasta;
        set { if (SetProperty(ref _hasta, value)) ItemsView.Refresh(); }
    }

    /// <summary>
    /// La tabla vacía por no haber cuentas y la tabla vacía por no haber movimientos son dos
    /// situaciones distintas, y el mensaje que saca de cada una también: en la primera hay que ir
    /// a crear una cuenta, en la segunda no falta nada.
    /// </summary>
    public string TituloVacio => HayCuentas ? "No hay movimientos en esta cuenta" : "No hay cuentas";

    public string DetalleVacio => HayCuentas
        ? "Los cobros y pagos aparecen solos al registrarlos en sus facturas. Lo que no viene de "
          + "un documento se agrega con «Nuevo movimiento»."
        : "Cree la primera cuenta en la pestaña Cuentas —el banco, la caja chica— con el saldo que "
          + "tiene hoy, y el libro empieza a contar desde ahí.";

    public decimal SaldoLibro =>
        CuentaSeleccionada is { } cuenta ? _servicio.SaldoDeLibro(cuenta.Id) : 0m;

    public decimal SaldoConciliado =>
        CuentaSeleccionada is { } cuenta ? _servicio.SaldoConciliado(cuenta.Id) : 0m;

    /// <summary>
    /// Lo que el libro dice y el banco todavía no confirmó: el cheque girado que nadie cobró, la
    /// transferencia que no ha caído. Cero quiere decir que la cuenta está cuadrada.
    /// </summary>
    public decimal DiferenciaConciliacion => SaldoLibro - SaldoConciliado;

    public string SaldoLibroTexto => SaldoLibro.ToString("N2");
    public string SaldoConciliadoTexto => SaldoConciliado.ToString("N2");
    public string DiferenciaTexto => DiferenciaConciliacion.ToString("N2");

    protected override string ModuloPermiso => "Banco";

    protected override bool CoincideBusqueda(MovimientoBanco item, string texto) =>
        item.Concepto.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Referencia.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.DocumentoTexto.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.CategoriaTexto.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(MovimientoBanco item)
    {
        if (CuentaSeleccionada is { } cuenta && item.CuentaId != cuenta.Id)
            return false;

        if (Desde is { } desde && item.Fecha.Date < desde.Date)
            return false;

        if (Hasta is { } hasta && item.Fecha.Date > hasta.Date)
            return false;

        return _filtroEstado switch
        {
            "Registrados" => item.Estado == EstadoMovimientoBanco.Registrado,
            "Conciliados" => item.Estado == EstadoMovimientoBanco.Conciliado,
            "Anulados" => item.Estado == EstadoMovimientoBanco.Anulado,
            "Entradas" => item.Tipo == TipoMovimientoBanco.Entrada,
            "Salidas" => item.Tipo == TipoMovimientoBanco.Salida,
            _ => true
        };
    }

    protected override bool PuedeEditar(MovimientoBanco item) => _servicio.PuedeEditar(item);

    protected override bool PuedeEliminar(MovimientoBanco item) => _servicio.PuedeEliminar(item);

    protected override MovimientoBanco CrearNuevo() => new()
    {
        Fecha = DateTime.Today,
        Tipo = TipoMovimientoBanco.Salida,
        Categoria = CategoriaMovimiento.GastoVario,
        Origen = OrigenMovimiento.Manual,
        Estado = EstadoMovimientoBanco.Registrado,
        CuentaId = CuentaSeleccionada?.Id ?? 0,
        CuentaNombre = CuentaSeleccionada?.Nombre ?? string.Empty,
        CreadoPorId = _sesionActual.UsuarioActual?.Id ?? 0,
        FechaCreacion = DateTime.Now
    };

    protected override CrudEditorViewModelBase<MovimientoBanco> CrearEditor(MovimientoBanco item) =>
        new MovimientoBancoEditorViewModel(item, _servicio.CuentasActivas(), _servicio);

    private void Conciliar()
    {
        if (SelectedItem is not { } movimiento)
            return;

        Aplicar(() => _servicio.Conciliar(movimiento, _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void Desconciliar()
    {
        if (SelectedItem is not { } movimiento)
            return;

        Aplicar(() => _servicio.Desconciliar(movimiento));
    }

    private void Anular()
    {
        if (SelectedItem is not { } movimiento)
            return;

        var aviso = movimiento.ContraparteId is null
            ? string.Empty
            : " Es media transferencia: se anulará también el movimiento de la otra cuenta.";

        var editor = new MotivoEditorViewModel(
            $"Anular movimiento Nº {movimiento.Id}",
            $"{movimiento.FechaTexto} — {movimiento.Concepto} — {movimiento.TipoTexto} {movimiento.MontoTexto}.{aviso}",
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(movimiento, editor.Motivo));
    }

    private void Transferir()
    {
        var editor = new TransferenciaEditorViewModel(_servicio.CuentasActivas(), CuentaSeleccionada);

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Transferir(
            editor.CuentaOrigen!.Id,
            editor.CuentaDestino!.Id,
            editor.MontoValor,
            editor.Fecha,
            editor.Concepto,
            editor.Referencia,
            _sesionActual.UsuarioActual?.Id ?? 0).Salida);
    }

    /// <summary>
    /// La lista la repuebla la recarga que dispara la escritura del servicio; aquí solo se apunta
    /// qué movimiento dejar seleccionado y se traduce el rechazo de una regla en un aviso.
    /// </summary>
    private void Aplicar(Func<MovimientoBanco> transicion)
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

    /// <summary>
    /// Relee los movimientos y, además, el catálogo de cuentas: dar de alta una cuenta en la otra
    /// pestaña tiene que dejarla disponible aquí sin salir y volver a entrar.
    /// </summary>
    public override void Recargar()
    {
        var cuentaId = CuentaSeleccionada?.Id;

        Cuentas.Clear();
        foreach (var cuenta in _cuentas.GetAll())
            Cuentas.Add(cuenta);

        // Sin notificar, el ComboBox se quedaría apuntando a la instancia vieja —la recarga trae
        // objetos nuevos— y la selección se vería vacía.
        _cuentaSeleccionada = Cuentas.FirstOrDefault(c => c.Id == cuentaId)
                              ?? Cuentas.FirstOrDefault(c => c.Activa)
                              ?? Cuentas.FirstOrDefault();
        OnPropertyChanged(nameof(CuentaSeleccionada));

        base.Recargar();

        CalcularSaldoCorrido();

        // El refresco va DESPUÉS de recalcular y no es opcional: los modelos no implementan
        // INotifyPropertyChanged, así que rellenar SaldoCorrido sobre filas que la tabla ya
        // enlazó no le llega a nadie. Refresh regenera las filas y vuelve a leer la propiedad.
        ItemsView.Refresh();
    }

    /// <summary>
    /// Rellena el saldo corrido de cada asiento de la cuenta mirada.
    ///
    /// Se calcula sobre TODA la historia de la cuenta y en orden cronológico, no sobre lo que se
    /// ve: filtrar por estado o buscar por texto esconde filas, y un saldo que solo sumara las
    /// visibles daría una cifra que no coincide con la del banco.
    /// </summary>
    private void CalcularSaldoCorrido()
    {
        if (CuentaSeleccionada is not { } cuenta)
            return;

        var saldo = cuenta.SaldoInicial;

        foreach (var movimiento in Items.Where(m => m.CuentaId == cuenta.Id)
                                        .OrderBy(m => m.Fecha)
                                        .ThenBy(m => m.Id))
        {
            saldo += movimiento.Efecto;
            movimiento.SaldoCorrido = saldo;
        }
    }
}

/// <summary>
/// El catálogo de cuentas del centro. Sub-listado de Finanzas · Banco.
/// </summary>
public sealed class CuentasBancariasCrudViewModel : CrudViewModelBase<CuentaBancaria, int>
{
    private readonly BancoService _servicio;
    private readonly IServicioDialogo _dialogos;

    public CuentasBancariasCrudViewModel(ICuentaBancariaDataSource cuentas,
                                         BancoService servicio,
                                         IServicioDialogo dialogos,
                                         ISesionActual sesion)
        : base(cuentas, dialogos, sesion)
    {
        _servicio = servicio;
        _dialogos = dialogos;

        RellenarSaldos();
    }

    protected override string ModuloPermiso => "CuentasBancarias";

    /// <summary>El disponible de todas las cuentas activas, que es la cifra que importa.</summary>
    public string ResumenDisponible => $"Disponible {_servicio.DisponibleTotal():N2}";

    public override void Recargar()
    {
        base.Recargar();
        RellenarSaldos();

        // Ver la nota de MovimientosBancoCrudViewModel.Recargar: sin Refresh, el saldo recién
        // calculado no llega a la tabla.
        ItemsView.Refresh();
    }

    /// <summary>
    /// Pone en cada fila el saldo de hoy de su cuenta. Va aquí y no en la entidad porque depende
    /// de la tabla de movimientos, que la fila de la cuenta no conoce.
    /// </summary>
    private void RellenarSaldos()
    {
        foreach (var cuenta in Items)
            cuenta.SaldoActual = _servicio.SaldoDeLibro(cuenta.Id);
    }

    protected override bool CoincideBusqueda(CuentaBancaria item, string texto) =>
        item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Banco.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.NumeroCuenta.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PuedeEliminar(CuentaBancaria item) => _servicio.PuedeEliminarCuenta(item);

    protected override CuentaBancaria CrearNuevo() => new()
    {
        FechaApertura = DateTime.Today,
        Moneda = "Bs",
        Activa = true
    };

    protected override CrudEditorViewModelBase<CuentaBancaria> CrearEditor(CuentaBancaria item) =>
        new CuentaBancariaEditorViewModel(item, _servicio);

    /// <summary>
    /// Borrar una cuenta con movimientos dejaría asientos citando algo que ya no existe. El
    /// servicio lo impide; aquí solo se explica por qué, en vez de dejar el botón gris sin decir
    /// nada.
    /// </summary>
    protected override void Eliminar()
    {
        if (SelectedItem is not { } cuenta)
            return;

        if (!_servicio.PuedeEliminarCuenta(cuenta))
        {
            _dialogos.Informar("No se puede eliminar la cuenta",
                $"{cuenta.Nombre} tiene movimientos registrados. Para dejar de usarla, edítela y "
                + "desmarque \"Activa\": deja de ofrecerse al registrar, y su historial se conserva.");
            return;
        }

        base.Eliminar();
    }
}

/// <summary>
/// Finanzas · Banco: el libro de entradas y salidas del centro y el catálogo de cuentas, en una
/// pantalla conmutable, con el mismo patrón que Cuentas por Pagar.
///
/// <b>El sistema no se conecta con ningún banco.</b> Es un libro interno: dice cuánto dinero
/// entró y salió por la aplicación, y la marca de conciliado es lo que sirve para cuadrarlo
/// contra el extracto que traiga el banco en papel.
/// </summary>
public sealed class BancoViewModel : PantallaViewModelBase
{
    public const string VistaMovimientos = "Movimientos";
    public const string VistaCuentas = "Cuentas";

    public BancoViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private BancoViewModel(Modulo modulo,
                           Submodulo submodulo,
                           IServicioDialogo dialogos,
                           ISesionActual sesion)
        : base(modulo, submodulo)
    {
        var cuentas = DataSourceFactory.CrearCuentasBancarias();
        var servicio = new BancoService(DataSourceFactory.CrearMovimientosBanco(), cuentas, sesion);

        Movimientos = new MovimientosBancoCrudViewModel(
            DataSourceFactory.CrearMovimientosBanco(), cuentas, servicio, dialogos, sesion);

        Cuentas = new CuentasBancariasCrudViewModel(cuentas, servicio, dialogos, sesion);

        CambiarVistaCommand = new RelayCommand<string>(vista => VistaActual = vista);
    }

    public MovimientosBancoCrudViewModel Movimientos { get; }
    public CuentasBancariasCrudViewModel Cuentas { get; }

    /// <summary>
    /// Los dos listados, aunque solo se vea uno: dar de alta una cuenta tiene que dejarla
    /// disponible al conmutar, y un movimiento nuevo cambia el saldo que muestra la otra pestaña.
    /// </summary>
    public override void Recargar()
    {
        Movimientos.Recargar();
        Cuentas.Recargar();
    }

    private string _vistaActual = VistaMovimientos;
    public string VistaActual
    {
        get => _vistaActual;
        set
        {
            if (SetProperty(ref _vistaActual, value))
                OnTodasLasPropiedadesCambiaron();
        }
    }

    public bool MostrarMovimientos => VistaActual == VistaMovimientos;
    public bool MostrarCuentas => VistaActual == VistaCuentas;

    /// <summary>
    /// Las dos pestañas, enlazadas en DOS VÍAS al <c>IsChecked</c> de su botón, como en
    /// <see cref="CuentasPorPagarViewModel"/>. El setter solo actúa al marcar: al desmarcar ya hay
    /// otro botón del grupo encendiéndose.
    /// </summary>
    public bool EsMovimientos
    {
        get => MostrarMovimientos;
        set { if (value) VistaActual = VistaMovimientos; }
    }

    public bool EsCuentas
    {
        get => MostrarCuentas;
        set { if (value) VistaActual = VistaCuentas; }
    }

    public ICommand CambiarVistaCommand { get; }
}
