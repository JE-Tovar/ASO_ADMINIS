using System.Collections.ObjectModel;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.Services.Fiscal;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Ventas · Cierre del día: el turno de la caja (abrir con fondo, cerrar con arqueo), el resumen
/// diario que calcula el sistema (por alícuota, exento, IGTF y medio de pago, neto de notas de
/// crédito) y, si el emisor es una máquina fiscal, los reportes X y Z con el historial de cierres
/// y si cuadraron.
///
/// Quien no tiene <see cref="Permisos.Ventas.VerTodasLasCajas"/> (el Cajero) ve solo lo de su
/// caja: su resumen, sus Z y sus turnos.
/// </summary>
public sealed class CierreViewModel : PantallaViewModelBase
{
    private readonly CierreService _servicio;
    private readonly TurnosService _turnos;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesion;

    public CierreViewModel(Modulo modulo, Submodulo submodulo)
        : base(modulo, submodulo)
    {
        _dialogos = new ServicioDialogo();
        _sesion = SesionActual.Instancia;
        _servicio = new CierreService(DataSourceFactory.CrearVentas(), DataSourceFactory.CrearCierresZ(),
                                      DataSourceFactory.CrearEmisorFiscal(), _sesion);
        _turnos = new TurnosService(DataSourceFactory.CrearTurnos(), DataSourceFactory.CrearVentas(), _sesion,
                                    DataSourceFactory.CrearCobrosCliente());

        ReporteXCommand = new RelayCommand(ReporteX, () => TieneXZ);
        ProbarEmisorCommand = new RelayCommand(ProbarEmisor);
        CierreZCommand = new RelayCommand(CierreZ, () => TieneXZ && _sesion.Puede(Permisos.Ventas.CierreZ));

        AbrirTurnoCommand = new RelayCommand(AbrirTurno,
            () => _sesion.CajaActual is not null && TurnoAbierto is null && _sesion.Puede(Permisos.Turnos.Abrir));
        CerrarTurnoCommand = new RelayCommand(() => CerrarTurno(TurnoAbierto),
            () => TurnoAbierto is { } t && PuedeCerrar(t));
        // Solo para turnos AJENOS: el propio se cierra con "Cerrar turno" de la tarjeta Mi turno.
        CerrarSeleccionadoCommand = new RelayCommand(() => CerrarTurno(TurnoSeleccionado),
            () => TurnoSeleccionado is { EstaAbierto: true } t
                  && t.UsuarioId != _sesion.UsuarioActual?.Id
                  && _sesion.Puede(Permisos.Turnos.CerrarAjeno));

        Recalcular();
    }

    public ICommand ReporteXCommand { get; }
    public ICommand ProbarEmisorCommand { get; }
    public ICommand CierreZCommand { get; }
    public ICommand AbrirTurnoCommand { get; }
    public ICommand CerrarTurnoCommand { get; }
    public ICommand CerrarSeleccionadoCommand { get; }

    public bool TieneXZ => _servicio.Emisor.TieneReportesXZ;

    /// <summary>La caja de la sesión (la del usuario, no la de la PC). Vacía si no tiene.</summary>
    public string Caja => _sesion.CajaActual?.Codigo ?? string.Empty;

    /// <summary>Cerrar el turno de OTRO cajero desde la grilla: supervisión o administración (<see cref="Permisos.Turnos.CerrarAjeno"/>).</summary>
    public bool PuedeCerrarAjenos => _sesion.Puede(Permisos.Turnos.CerrarAjeno);

    /// <summary>Quien ve todas las cajas ve el resumen y los turnos de todas; el cajero, los de la suya.</summary>
    public bool VeTodasLasCajas => _sesion.Puede(Permisos.Ventas.VerTodasLasCajas);

    public string EmisorTexto => TieneXZ
        ? $"Emisor: {_servicio.Emisor.Nombre}. Tu caja: {(string.IsNullOrWhiteSpace(Caja) ? "sin asignar" : Caja)}."
        : $"Emisor: {_servicio.Emisor.Nombre}. Una imprenta digital no tiene reportes X ni Z: el cierre es el resumen diario.";

    public string ResumenTitulo => VeTodasLasCajas || string.IsNullOrWhiteSpace(Caja)
        ? "Resumen diario (Bs)"
        : $"Resumen diario de {Caja} (Bs)";

    private DateTime? _dia = DateTime.Today;
    public DateTime? Dia
    {
        get => _dia;
        set { if (SetProperty(ref _dia, value)) Recalcular(); }
    }

    public ObservableCollection<FilaResumen> Resumen { get; } = [];
    public ObservableCollection<FilaResumen> PorMetodo { get; } = [];
    public ObservableCollection<CierreZ> Historial { get; } = [];
    public ObservableCollection<Turno> Turnos { get; } = [];

    private string _documentosTexto = string.Empty;
    public string DocumentosTexto
    {
        get => _documentosTexto;
        private set => SetProperty(ref _documentosTexto, value);
    }

    // ---------------------------------------------------------------- Turno

    private Turno? _turnoAbierto;

    /// <summary>El turno abierto en la caja de la sesión, sea de quien sea.</summary>
    public Turno? TurnoAbierto
    {
        get => _turnoAbierto;
        private set
        {
            _turnoAbierto = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TurnoTexto));
            OnPropertyChanged(nameof(TurnoDetalle));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private Turno? _turnoSeleccionado;
    public Turno? TurnoSeleccionado
    {
        get => _turnoSeleccionado;
        set { if (SetProperty(ref _turnoSeleccionado, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public string TurnoTexto => _sesion.CajaActual is not { } caja
        ? "Sin caja asignada"
        : TurnoAbierto is not { } t
            ? $"{caja.Codigo} · sin turno abierto"
            : t.UsuarioId == _sesion.UsuarioActual?.Id
                ? $"{caja.Codigo} · tu turno, abierto desde las {t.Apertura:HH:mm}"
                : $"{caja.Codigo} · turno de {t.UsuarioNombre}, abierto desde las {t.Apertura:HH:mm}";

    public string TurnoDetalle => _sesion.CajaActual is null
        ? "Para vender necesitas una caja: la asigna el administrador en Administración · Usuarios."
        : TurnoAbierto is not { } t
            ? "Abre el turno con el fondo de la gaveta para poder facturar."
            : $"Fondo inicial: {t.FondoTexto}. Al cerrar se cuenta el efectivo y se compara con lo esperado.";

    private bool PuedeCerrar(Turno t) =>
        _sesion.Puede(t.UsuarioId == _sesion.UsuarioActual?.Id ? Permisos.Turnos.Cerrar : Permisos.Turnos.CerrarAjeno);

    private void AbrirTurno()
    {
        if (_sesion.CajaActual is not { } caja)
            return;

        var editor = new AbrirTurnoEditorViewModel(caja, _sesion.UsuarioActual?.NombreCompleto ?? string.Empty);
        if (!_dialogos.MostrarEditor(editor))
            return;

        try
        {
            _turnos.Abrir(editor.FondoBsValor, editor.FondoDivisaValor);
            Recalcular();
        }
        catch (InvalidOperationException ex)
        {
            Recalcular();
            _dialogos.Informar("No se pudo abrir el turno", ex.Message);
        }
    }

    private void CerrarTurno(Turno? turno)
    {
        if (turno is null)
            return;

        var (bs, divisa) = _turnos.Esperado(turno);
        var editor = new CerrarTurnoEditorViewModel(turno, bs, divisa,
                                                    esAjeno: turno.UsuarioId != _sesion.UsuarioActual?.Id);
        if (!_dialogos.MostrarEditor(editor))
            return;

        try
        {
            var cerrado = _turnos.Cerrar(turno, editor.ContadoBsValor, editor.ContadoDivisaValor, editor.Observaciones);
            Recalcular();
            _dialogos.Informar($"Turno cerrado · {cerrado.CajaCodigo}",
                $"Esperado: Bs. {cerrado.EsperadoBs:N2} · US$ {cerrado.EsperadoDivisa:N2}\n" +
                $"Contado: Bs. {cerrado.ContadoBs:N2} · US$ {cerrado.ContadoDivisa:N2}\n\n" +
                (cerrado.Cuadra ? "Cuadra." : $"Diferencia: {cerrado.DiferenciaTexto}"));
        }
        catch (InvalidOperationException ex)
        {
            Recalcular();
            _dialogos.Informar("No se pudo cerrar el turno", ex.Message);
        }
    }

    // ---------------------------------------------------------------- Resumen

    public override void Recargar() => Recalcular();

    private void Recalcular()
    {
        // El cajero ve el resumen de SU caja; quien supervisa, el de todas.
        var r = _servicio.ResumenDiario(Dia ?? DateTime.Today, VeTodasLasCajas ? null : Caja);

        DocumentosTexto = $"{r.Facturas} facturas · {r.NotasCredito} notas de crédito · {r.NotasDebito} notas de débito";

        Resumen.Clear();
        foreach (var fila in FilasDe(r.Exento, r.BaseGeneral, r.IvaGeneral, r.BaseReducida, r.IvaReducida,
                                     r.BaseAdicional, r.IvaAdicional, r.Igtf, r.Total))
            Resumen.Add(fila);

        PorMetodo.Clear();
        foreach (var (metodo, bs, divisa) in r.PorMetodo)
            PorMetodo.Add(new FilaResumen(metodo, bs.ToString("N2") + (divisa > 0 ? $"  (US$ {divisa:N2})" : "")));

        Historial.Clear();
        foreach (var c in _servicio.Historial().Where(c => VeTodasLasCajas || c.CajaCodigo == Caja))
            Historial.Add(c);

        TurnoAbierto = _turnos.TurnoAbierto();

        var seleccionado = TurnoSeleccionado?.Id;
        Turnos.Clear();
        foreach (var t in _turnos.Historial().Take(200))
            Turnos.Add(t);
        TurnoSeleccionado = Turnos.FirstOrDefault(t => t.Id == seleccionado);
    }

    private static IEnumerable<FilaResumen> FilasDe(decimal exento, decimal bg, decimal ig, decimal br, decimal ir,
                                                    decimal ba, decimal ia, decimal igtf, decimal total) =>
    [
        new("Exento", exento.ToString("N2")),
        new("Base imponible general", bg.ToString("N2")),
        new("IVA general", ig.ToString("N2")),
        new("Base imponible reducida", br.ToString("N2")),
        new("IVA reducida", ir.ToString("N2")),
        new("Base imponible adicional", ba.ToString("N2")),
        new("IVA adicional", ia.ToString("N2")),
        new("IGTF percibido", igtf.ToString("N2")),
        new("Total", total.ToString("N2"))
    ];

    /// <summary>¿Responde el emisor de ESTA PC? Sin emitir nada. Es lo primero que se mira cuando falla una factura.</summary>
    private void ProbarEmisor()
    {
        EstadoEmisor estado;
        try
        {
            estado = _servicio.Emisor.ProbarConexion();
        }
        catch (Exception ex)
        {
            estado = new EstadoEmisor(false, ex.Message);
        }

        Registro.Info("Prueba del emisor fiscal: {Disponible} · {Detalle}", estado.Disponible, estado.Detalle);
        _dialogos.Informar(estado.Disponible ? "El emisor responde" : "El emisor NO responde",
            $"{_servicio.Emisor.Nombre}\n{estado.Detalle}" +
            (estado.Serial is { Length: > 0 } serial ? $"\nSerial: {serial}" : string.Empty) +
            (_servicio.Emisor.EsFiscal ? string.Empty : "\n\nSIN VALOR FISCAL: lo que emite es de prueba."));
    }

    private void ReporteX()
    {
        try
        {
            var x = _servicio.ReporteX(Caja);
            _dialogos.Informar("Reporte X", Texto(x));
        }
        catch (Exception ex) when (ex is InvalidOperationException or EmisorFiscalException)
        {
            _dialogos.Informar("No se pudo leer el reporte X", ex.Message);
        }
    }

    private void CierreZ()
    {
        if (!_dialogos.Confirmar("Cierre Z",
                "El reporte Z cierra el día en la máquina fiscal y pone a cero sus acumulados. No se puede deshacer. ¿Continuar?"))
            return;

        try
        {
            var cierre = _servicio.CerrarZ(Caja);
            Recalcular();
            _dialogos.Informar($"Reporte Z N° {cierre.NumeroZ}",
                $"Máquina: {cierre.Documentos} documentos · Bs. {cierre.TotalTexto}\n" +
                $"Sistema: {cierre.DocumentosSistema} documentos · Bs. {cierre.TotalSistemaTexto}\n\n" +
                (cierre.Cuadra ? "Cuadra." : $"NO CUADRA ({cierre.CuadraTexto}). Revise los documentos del día y el diario fiscal."));
        }
        catch (Exception ex) when (ex is InvalidOperationException or EmisorFiscalException)
        {
            _dialogos.Informar("No se pudo hacer el cierre Z", ex.Message);
        }
    }

    private static string Texto(ResumenFiscal r) =>
        $"Documentos: {r.Documentos}\n" +
        $"Exento: {r.Exento:N2}\n" +
        $"BI general: {r.BaseGeneral:N2} · IVA {r.IvaGeneral:N2}\n" +
        $"BI reducida: {r.BaseReducida:N2} · IVA {r.IvaReducida:N2}\n" +
        $"BI adicional: {r.BaseAdicional:N2} · IVA {r.IvaAdicional:N2}\n" +
        $"IGTF: {r.Igtf:N2}\n" +
        $"Total: Bs. {r.Total:N2}";
}

/// <summary>Una fila etiqueta / valor de las tablas del cierre.</summary>
public sealed record FilaResumen(string Concepto, string Valor);
