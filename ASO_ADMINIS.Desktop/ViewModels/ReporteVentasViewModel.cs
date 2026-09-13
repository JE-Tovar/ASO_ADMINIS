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
/// Reportes · Ventas: lo vendido en un rango de fechas, con sus totales y dos gráficos — por día
/// (columnas) y por categoría de producto (torta). Es de solo lectura: no hereda de
/// <c>CrudViewModelBase</c> porque no da de alta ni edita nada, solo muestra lo que ya
/// registró Ventas · Registro.
/// </summary>
public sealed class ReporteVentasViewModel : PantallaViewModelBase
{
    private readonly IVentaDataSource _ventas;
    private readonly IModeloDataSource _modelos;

    public ReporteVentasViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearVentas(), DataSourceFactory.CrearModelos())
    {
    }

    private ReporteVentasViewModel(Modulo modulo,
                                   Submodulo submodulo,
                                   IVentaDataSource ventas,
                                   IModeloDataSource modelos)
        : base(modulo, submodulo)
    {
        _ventas = ventas;
        _modelos = modelos;

        _desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _hasta = DateTime.Today;

        Recalcular();
    }

    public ObservableCollection<Venta> Items { get; } = [];

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

    private string _totalUsdTexto = "0,00";
    public string TotalUsdTexto
    {
        get => _totalUsdTexto;
        private set => SetProperty(ref _totalUsdTexto, value);
    }

    private string _totalBsTexto = "0,00";
    public string TotalBsTexto
    {
        get => _totalBsTexto;
        private set => SetProperty(ref _totalBsTexto, value);
    }

    private string _totalUnidadesTexto = "0";
    public string TotalUnidadesTexto
    {
        get => _totalUnidadesTexto;
        private set => SetProperty(ref _totalUnidadesTexto, value);
    }

    private PlotModel _graficoPorDia = new();
    public PlotModel GraficoPorDia
    {
        get => _graficoPorDia;
        private set => SetProperty(ref _graficoPorDia, value);
    }

    private PlotModel _graficoPorCategoria = new();
    public PlotModel GraficoPorCategoria
    {
        get => _graficoPorCategoria;
        private set => SetProperty(ref _graficoPorCategoria, value);
    }

    public override void Recargar() => Recalcular();

    private void Recalcular()
    {
        var modelosPorId = _modelos.GetAll().ToDictionary(m => m.Id);

        var enRango = _ventas.GetAll()
            .Where(v => v.Estado == EstadoVenta.Registrada)
            .Where(v => Desde is not { } desde || v.Fecha.Date >= desde.Date)
            .Where(v => Hasta is not { } hasta || v.Fecha.Date <= hasta.Date)
            .OrderBy(v => v.Fecha)
            .ToList();

        Items.Clear();
        foreach (var venta in enRango)
            Items.Add(venta);

        TotalUsdTexto = enRango.Sum(v => v.TotalUsd).ToString("N2");
        TotalBsTexto = enRango.Sum(v => v.TotalBs).ToString("N2");
        TotalUnidadesTexto = enRango.Sum(v => v.Cantidad).ToString("N2");

        GraficoPorDia = ConstruirGraficoPorDia(enRango);
        GraficoPorCategoria = ConstruirGraficoPorCategoria(enRango, modelosPorId);
    }

    /// <summary>
    /// Barras horizontales y no columnas: <c>ColumnSeries</c> se retiró de OxyPlot 2.x
    /// (ver github.com/oxyplot/oxyplot/discussions/2063) y transponer <c>BarSeries</c> con
    /// <c>XAxisKey</c>/<c>YAxisKey</c> agrega una complejidad que esto no necesita — una barra
    /// horizontal por día se lee igual de bien.
    /// </summary>
    private static PlotModel ConstruirGraficoPorDia(IReadOnlyList<Venta> ventas)
    {
        var modelo = new PlotModel { Title = "Ventas por día (US$)" };
        var categorias = new CategoryAxis { Position = AxisPosition.Left };
        var serie = new BarSeries { LabelPlacement = LabelPlacement.Outside, LabelFormatString = "{0:N0}" };

        foreach (var grupo in ventas.GroupBy(v => v.Fecha.Date).OrderBy(g => g.Key))
        {
            categorias.Labels.Add(grupo.Key.ToString("dd/MM"));
            serie.Items.Add(new BarItem { Value = (double)grupo.Sum(v => v.TotalUsd) });
        }

        modelo.Axes.Add(categorias);
        modelo.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, MinimumPadding = 0, AbsoluteMinimum = 0 });
        modelo.Series.Add(serie);
        return modelo;
    }

    private static PlotModel ConstruirGraficoPorCategoria(IReadOnlyList<Venta> ventas,
                                                           IReadOnlyDictionary<int, Modelo> modelosPorId)
    {
        var modelo = new PlotModel { Title = "Ventas por categoría" };
        var serie = new PieSeries { StrokeThickness = 1, InsideLabelPosition = 0.7 };

        foreach (var grupo in ventas.GroupBy(v =>
                     modelosPorId.TryGetValue(v.ModeloId, out var m) ? m.CategoriaTexto : "Otro"))
            serie.Slices.Add(new PieSlice(grupo.Key, (double)grupo.Sum(v => v.TotalUsd)));

        modelo.Series.Add(serie);
        return modelo;
    }
}
