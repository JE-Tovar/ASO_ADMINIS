using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Reportes · Gastos: lo pagado a proveedores en un rango de fechas. "Gasto" es el pago real
/// (<see cref="EstadoFacturaProveedor.Pagada"/>, por <see cref="FacturaProveedor.FechaPago"/>),
/// no lo facturado y todavía pendiente — eso sigue siendo una deuda, no un gasto.
/// </summary>
public sealed class ReporteGastosViewModel : PantallaViewModelBase
{
    private readonly IFacturaProveedorDataSource _facturas;

    public ReporteGastosViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearFacturasProveedor())
    {
    }

    private ReporteGastosViewModel(Modulo modulo, Submodulo submodulo, IFacturaProveedorDataSource facturas)
        : base(modulo, submodulo)
    {
        _facturas = facturas;

        _desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _hasta = DateTime.Today;

        Recalcular();
    }

    public ObservableCollection<FacturaProveedor> Items { get; } = [];

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

    private PlotModel _graficoPorMes = new();
    public PlotModel GraficoPorMes
    {
        get => _graficoPorMes;
        private set => SetProperty(ref _graficoPorMes, value);
    }

    private PlotModel _graficoPorProveedor = new();
    public PlotModel GraficoPorProveedor
    {
        get => _graficoPorProveedor;
        private set => SetProperty(ref _graficoPorProveedor, value);
    }

    public override void Recargar() => Recalcular();

    private void Recalcular()
    {
        var pagadas = _facturas.GetAll()
            .Where(f => f.Estado == EstadoFacturaProveedor.Pagada && f.FechaPago.HasValue)
            .Where(f => Desde is not { } desde || f.FechaPago!.Value.Date >= desde.Date)
            .Where(f => Hasta is not { } hasta || f.FechaPago!.Value.Date <= hasta.Date)
            .OrderBy(f => f.FechaPago)
            .ToList();

        Items.Clear();
        foreach (var factura in pagadas)
            Items.Add(factura);

        TotalTexto = pagadas.Sum(f => f.Monto).ToString("N2");

        GraficoPorMes = ConstruirGraficoPorMes(pagadas);
        GraficoPorProveedor = ConstruirGraficoPorProveedor(pagadas);
    }

    /// <summary>Barras horizontales: ver la nota equivalente en <c>ReporteVentasViewModel</c> sobre por qué no <c>ColumnSeries</c>.</summary>
    private static PlotModel ConstruirGraficoPorMes(IReadOnlyList<FacturaProveedor> facturas)
    {
        var modelo = new PlotModel { Title = "Gastos por mes" };
        var categorias = new CategoryAxis { Position = AxisPosition.Left };
        var serie = new BarSeries { LabelPlacement = LabelPlacement.Outside, LabelFormatString = "{0:N0}" };

        foreach (var grupo in facturas
                     .GroupBy(f => new DateTime(f.FechaPago!.Value.Year, f.FechaPago.Value.Month, 1))
                     .OrderBy(g => g.Key))
        {
            categorias.Labels.Add(grupo.Key.ToString("MM/yyyy"));
            serie.Items.Add(new BarItem { Value = (double)grupo.Sum(f => f.Monto) });
        }

        modelo.Axes.Add(categorias);
        modelo.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, MinimumPadding = 0, AbsoluteMinimum = 0 });
        modelo.Series.Add(serie);
        return modelo;
    }

    private static PlotModel ConstruirGraficoPorProveedor(IReadOnlyList<FacturaProveedor> facturas)
    {
        var modelo = new PlotModel { Title = "Gastos por proveedor" };
        var serie = new PieSeries { StrokeThickness = 1, InsideLabelPosition = 0.7 };

        foreach (var grupo in facturas.GroupBy(f => f.ProveedorNombre))
            serie.Slices.Add(new PieSlice(grupo.Key, (double)grupo.Sum(f => f.Monto)));

        modelo.Series.Add(serie);
        return modelo;
    }
}
