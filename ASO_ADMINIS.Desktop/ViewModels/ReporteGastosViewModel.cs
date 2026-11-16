using System.Collections.ObjectModel;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;
using OxyPlot;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Reportes · Gastos: lo que la empresa pagó en el período, en Bs — pagos a proveedores y gastos
/// varios juntos. Las reglas de qué cuenta y cómo se convierte están en
/// <see cref="ReporteGastosService"/>; aquí solo se filtra, se totaliza y se grafica.
/// </summary>
public sealed class ReporteGastosViewModel : PantallaViewModelBase
{
    private const string FiltroTodos = "Todos";
    private const string FiltroProveedores = "Proveedores";
    private const string FiltroVarios = "Gastos varios";

    private readonly ReporteGastosService _servicio;
    private readonly IFacturaProveedorDataSource _facturas;
    private readonly IGastoVarioDataSource _gastos;

    public ReporteGastosViewModel(Modulo modulo, Submodulo submodulo)
        : base(modulo, submodulo)
    {
        _facturas = DataSourceFactory.CrearFacturasProveedor();
        _gastos = DataSourceFactory.CrearGastosVarios();
        _servicio = new ReporteGastosService(_facturas,
                                             _gastos,
                                             DataSourceFactory.CrearMovimientosBanco(),
                                             DataSourceFactory.CrearCuentasBancarias(),
                                             DataSourceFactory.CrearTasasCambio());

        _desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _hasta = DateTime.Today;

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro ?? FiltroTodos;
            Recalcular();
        });

        AbrirCommand = new RelayCommand(Abrir, () => Seleccionada is not null);

        Recalcular();
    }

    public ObservableCollection<FilaGasto> Items { get; } = [];

    private FilaGasto? _seleccionada;
    public FilaGasto? Seleccionada
    {
        get => _seleccionada;
        set => SetProperty(ref _seleccionada, value);
    }

    /// <summary>
    /// Doble clic (o Enter) en una fila: el documento de origen en su ficha de solo lectura. Un
    /// pago a proveedor abre su factura, con los renglones de lo que se compró (los copia de la
    /// entrada de almacén al registrarla); un gasto vario abre el gasto. Solo pide acceso a esta
    /// pantalla, como toda ficha.
    /// </summary>
    public ICommand AbrirCommand { get; }

    private void Abrir()
    {
        if (Seleccionada is not { } fila)
            return;

        var dialogo = new ServicioDialogo();
        CrudEditorViewModelBase? ficha = fila.Origen == OrigenGasto.Proveedor
            ? _facturas.GetById(fila.DocumentoId) is { } factura ? new FacturaProveedorDetalleViewModel(factura) : null
            : _gastos.GetById(fila.DocumentoId) is { } gasto ? new GastoVarioDetalleViewModel(gasto) : null;

        if (ficha is null)
            dialogo.Informar("Detalle", $"No se encontró el documento {fila.Documento}: puede que se haya eliminado.");
        else
            dialogo.MostrarEditor(ficha);
    }

    public ICommand CambiarFiltroCommand { get; }

    private string _filtro = FiltroTodos;

    private DateTime? _desde;
    public DateTime? Desde
    {
        get => _desde;
        set { if (SetProperty(ref _desde, value)) Recalcular(); }
    }

    private DateTime? _hasta;
    public DateTime? Hasta
    {
        get => _hasta;
        set { if (SetProperty(ref _hasta, value)) Recalcular(); }
    }

    private string _totalTexto = "0,00";
    public string TotalTexto
    {
        get => _totalTexto;
        private set => SetProperty(ref _totalTexto, value);
    }

    private string _proveedoresTexto = "0,00";
    public string ProveedoresTexto
    {
        get => _proveedoresTexto;
        private set => SetProperty(ref _proveedoresTexto, value);
    }

    private string _variosTexto = "0,00";
    public string VariosTexto
    {
        get => _variosTexto;
        private set => SetProperty(ref _variosTexto, value);
    }

    private string _cantidadVariosTexto = "0";
    public string CantidadVariosTexto
    {
        get => _cantidadVariosTexto;
        private set => SetProperty(ref _cantidadVariosTexto, value);
    }

    private PlotModel _graficoPorMes = new();
    public PlotModel GraficoPorMes
    {
        get => _graficoPorMes;
        private set => SetProperty(ref _graficoPorMes, value);
    }

    private GraficoTorta _graficoPorCategoria = GraficoTorta.Vacio("Gastos por categoría (Bs)");
    public GraficoTorta GraficoPorCategoria
    {
        get => _graficoPorCategoria;
        private set => SetProperty(ref _graficoPorCategoria, value);
    }

    public override void Recargar() => Recalcular();

    private void Recalcular()
    {
        var todas = _servicio.Filas(Desde, Hasta);

        // Los totales de la barra son siempre del período completo; el filtro de origen solo
        // decide qué se lista y grafica.
        var proveedores = todas.Where(f => f.Origen == OrigenGasto.Proveedor).ToList();
        var varios = todas.Where(f => f.Origen == OrigenGasto.GastoVario).ToList();
        TotalTexto = todas.Sum(f => f.MontoBs).ToString("N2");
        ProveedoresTexto = proveedores.Sum(f => f.MontoBs).ToString("N2");
        VariosTexto = varios.Sum(f => f.MontoBs).ToString("N2");
        CantidadVariosTexto = varios.Count.ToString();

        var visibles = _filtro switch
        {
            FiltroProveedores => proveedores,
            FiltroVarios => varios,
            _ => todas
        };

        Items.Clear();
        foreach (var fila in visibles)
            Items.Add(fila);

        GraficoPorMes = ConstruirGraficoPorMes(visibles);
        GraficoPorCategoria = Graficos.Torta("Gastos por categoría (Bs)",
            visibles.GroupBy(f => f.Categoria, StringComparer.OrdinalIgnoreCase)
                    .Select(g => (g.Key, (double)g.Sum(f => f.MontoBs))));
    }

    /// <summary>Una barra por mes, partida en proveedores y gastos varios.</summary>
    private static PlotModel ConstruirGraficoPorMes(IReadOnlyList<FilaGasto> filas)
    {
        var meses = filas.Select(f => new DateTime(f.Fecha.Year, f.Fecha.Month, 1))
                         .Distinct()
                         .OrderBy(m => m)
                         .ToList();

        IReadOnlyList<double> Serie(OrigenGasto origen) =>
            [.. meses.Select(m => (double)filas.Where(f => f.Origen == origen
                                                          && f.Fecha.Year == m.Year && f.Fecha.Month == m.Month)
                                               .Sum(f => f.MontoBs))];

        return Graficos.BarrasApiladas("Gastos por mes (Bs)",
            [.. meses.Select(m => m.ToString("MM/yyyy"))],
            [("Proveedores", Serie(OrigenGasto.Proveedor)), ("Gastos varios", Serie(OrigenGasto.GastoVario))]);
    }
}
