using System.Collections.ObjectModel;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.Services.Fiscal;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Ventas · Cierre del día: el resumen diario que calcula el sistema (por alícuota, exento,
/// IGTF y medio de pago, neto de notas de crédito) y, si el emisor es una máquina fiscal, los
/// reportes X y Z con el historial de cierres y si cuadraron.
/// </summary>
public sealed class CierreViewModel : PantallaViewModelBase
{
    private readonly CierreService _servicio;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesion;

    public CierreViewModel(Modulo modulo, Submodulo submodulo)
        : base(modulo, submodulo)
    {
        _dialogos = new ServicioDialogo();
        _sesion = SesionActual.Instancia;
        _servicio = new CierreService(DataSourceFactory.CrearVentas(), DataSourceFactory.CrearCierresZ(),
                                      DataSourceFactory.CrearEmisorFiscal(), _sesion);

        ReporteXCommand = new RelayCommand(ReporteX, () => TieneXZ);
        CierreZCommand = new RelayCommand(CierreZ, () => TieneXZ && _sesion.Puede(Permisos.Ventas.CierreZ));

        Recalcular();
    }

    public ICommand ReporteXCommand { get; }
    public ICommand CierreZCommand { get; }

    public bool TieneXZ => _servicio.Emisor.TieneReportesXZ;

    public string Caja => Ajustes.Actual.CajaCodigo;

    public string EmisorTexto => TieneXZ
        ? $"Emisor: {_servicio.Emisor.Nombre}. Caja de esta máquina: {(string.IsNullOrWhiteSpace(Caja) ? "sin asignar" : Caja)}."
        : $"Emisor: {_servicio.Emisor.Nombre}. Una imprenta digital no tiene reportes X ni Z: el cierre es el resumen diario.";

    private DateTime? _dia = DateTime.Today;
    public DateTime? Dia
    {
        get => _dia;
        set { if (SetProperty(ref _dia, value)) Recalcular(); }
    }

    public ObservableCollection<FilaResumen> Resumen { get; } = [];
    public ObservableCollection<FilaResumen> PorMetodo { get; } = [];
    public ObservableCollection<CierreZ> Historial { get; } = [];

    private string _documentosTexto = string.Empty;
    public string DocumentosTexto
    {
        get => _documentosTexto;
        private set => SetProperty(ref _documentosTexto, value);
    }

    public override void Recargar() => Recalcular();

    private void Recalcular()
    {
        var r = _servicio.ResumenDiario(Dia ?? DateTime.Today);

        DocumentosTexto = $"{r.Facturas} facturas · {r.NotasCredito} notas de crédito · {r.NotasDebito} notas de débito";

        Resumen.Clear();
        foreach (var fila in FilasDe(r.Exento, r.BaseGeneral, r.IvaGeneral, r.BaseReducida, r.IvaReducida,
                                     r.BaseAdicional, r.IvaAdicional, r.Igtf, r.Total))
            Resumen.Add(fila);

        PorMetodo.Clear();
        foreach (var (metodo, bs, divisa) in r.PorMetodo)
            PorMetodo.Add(new FilaResumen(metodo, bs.ToString("N2") + (divisa > 0 ? $"  (US$ {divisa:N2})" : "")));

        Historial.Clear();
        foreach (var c in _servicio.Historial())
            Historial.Add(c);
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
