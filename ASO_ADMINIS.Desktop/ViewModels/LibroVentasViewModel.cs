using System.Collections.ObjectModel;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Reportes · Libro de ventas: el libro fiscal del período, listo para el contador.
///
/// El período es el mes, o la quincena si la organización es contribuyente especial (declara
/// el IVA por quincena). Se exporta a CSV; el armado (consolidar consumidor final por Z, notas
/// en negativo, retenciones) es de <see cref="LibroVentasService"/>.
/// </summary>
public sealed class LibroVentasViewModel : PantallaViewModelBase
{
    private readonly LibroVentasService _servicio;
    private readonly IServicioDialogo _dialogos;
    private LibroVentas? _libro;

    public LibroVentasViewModel(Modulo modulo, Submodulo submodulo)
        : base(modulo, submodulo)
    {
        _servicio = new LibroVentasService(DataSourceFactory.CrearVentas(), DataSourceFactory.CrearRetencionesIva());
        _dialogos = new ServicioDialogo();

        var hoy = DateTime.Today;
        Anios = [.. Enumerable.Range(hoy.Year - 5, 6).Reverse()];
        _anio = hoy.Year;
        _mes = hoy.Month;
        PorQuincena = Ambito.Actual?.ContribuyenteEspecial == true;
        _quincena = PorQuincena ? (hoy.Day <= 15 ? Quincenas[0] : Quincenas[1]) : Quincenas[2];

        ExportarCommand = new RelayCommand(Exportar, () => _libro is { Filas.Count: > 0 });
        Recalcular();
    }

    public ICommand ExportarCommand { get; }

    public IReadOnlyList<int> Anios { get; }

    public IReadOnlyList<string> Meses { get; } =
        [.. Enumerable.Range(1, 12).Select(m => new DateTime(2000, m, 1).ToString("MMMM", System.Globalization.CultureInfo.GetCultureInfo("es-VE")))];

    public string[] Quincenas { get; } = ["1ª quincena (1–15)", "2ª quincena (16–fin)", "Mes completo"];

    /// <summary>Contribuyente especial: declara por quincena.</summary>
    public bool PorQuincena { get; }

    public string PeriodoAyuda => PorQuincena
        ? "Contribuyente especial: el IVA se declara por quincena."
        : "Contribuyente ordinario: el IVA se declara por mes.";

    private int _anio;
    public int Anio
    {
        get => _anio;
        set { if (SetProperty(ref _anio, value)) Recalcular(); }
    }

    private int _mes;

    /// <summary>Índice 0–11 para el ComboBox de meses.</summary>
    public int MesIndice
    {
        get => _mes - 1;
        set { if (value >= 0 && SetProperty(ref _mes, value + 1)) Recalcular(); }
    }

    private string _quincena;
    public string Quincena
    {
        get => _quincena;
        set { if (SetProperty(ref _quincena, value)) Recalcular(); }
    }

    public ObservableCollection<FilaLibroVentas> Filas { get; } = [];

    private FilaLibroVentas _totales = new();
    public FilaLibroVentas Totales
    {
        get => _totales;
        private set => SetProperty(ref _totales, value);
    }

    private string _resumenTexto = string.Empty;
    public string ResumenTexto
    {
        get => _resumenTexto;
        private set => SetProperty(ref _resumenTexto, value);
    }

    public override void Recargar() => Recalcular();

    private (DateTime Desde, DateTime Hasta) Periodo()
    {
        var inicio = new DateTime(Anio, _mes, 1);
        var fin = inicio.AddMonths(1).AddDays(-1);
        return Array.IndexOf(Quincenas, Quincena) switch
        {
            0 => (inicio, new DateTime(Anio, _mes, 15)),
            1 => (new DateTime(Anio, _mes, 16), fin),
            _ => (inicio, fin)
        };
    }

    private void Recalcular()
    {
        var (desde, hasta) = Periodo();
        _libro = _servicio.Generar(desde, hasta);

        Filas.Clear();
        foreach (var fila in _libro.Filas)
            Filas.Add(fila);

        Totales = _libro.Totales;
        ResumenTexto = $"{desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy} · {_libro.Filas.Count} operaciones · " +
                       $"Débito fiscal Bs. {_libro.DebitoFiscal:N2} · IGTF Bs. {_libro.Totales.Igtf:N2} · " +
                       $"IVA retenido Bs. {_libro.Totales.IvaRetenido:N2}";
        CommandManager.InvalidateRequerySuggested();
    }

    private void Exportar()
    {
        if (_libro is null || Ambito.Actual is not { } org)
            return;

        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar libro de ventas",
            Filter = "CSV (separado por punto y coma)|*.csv",
            FileName = $"LibroVentas_{_libro.Desde:yyyyMMdd}_{_libro.Hasta:yyyyMMdd}.csv"
        };

        if (dialogo.ShowDialog() != true)
            return;

        try
        {
            LibroVentasService.ExportarCsv(_libro, org, dialogo.FileName);
            _dialogos.Informar("Libro exportado", $"Se guardó en:\n{dialogo.FileName}");
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            _dialogos.Informar("No se pudo exportar", ex.Message);
        }
    }
}
