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
    private readonly IArticuloDataSource _articulos;

    public ReporteVentasViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearVentas(), DataSourceFactory.CrearArticulos())
    {
    }

    private ReporteVentasViewModel(Modulo modulo,
                                   Submodulo submodulo,
                                   IVentaDataSource ventas,
                                   IArticuloDataSource articulos)
        : base(modulo, submodulo)
    {
        _ventas = ventas;
        _articulos = articulos;

        _desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _hasta = DateTime.Today;

        Recalcular();
    }

    public ObservableCollection<Venta> Items { get; } = [];

    /// <summary>Cuánto entró por cada método de pago en el período: lo que se mira al cuadrar caja.</summary>
    public ObservableCollection<ResumenMetodoPago> PorMetodoPago { get; } = [];

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
        var articulosPorId = _articulos.GetAll().ToDictionary(a => a.Id);

        // Documentos vigentes (facturas y notas; las ventas históricas anuladas no cuentan).
        // Las notas de crédito restan: todo se suma con su Signo.
        var enRango = _ventas.GetAll()
            .Where(v => v.Vigente)
            .Where(v => Desde is not { } desde || v.Fecha.Date >= desde.Date)
            .Where(v => Hasta is not { } hasta || v.Fecha.Date <= hasta.Date)
            .OrderBy(v => v.Fecha)
            .ToList();

        Items.Clear();
        foreach (var venta in enRango)
            Items.Add(venta);

        TotalUsdTexto = enRango.Sum(v => v.Signo * v.TotalUsd).ToString("N2");
        TotalBsTexto = enRango.Sum(v => v.Signo * v.Total).ToString("N2");
        TotalUnidadesTexto = enRango.Where(v => v.Tipo != TipoDocumentoVenta.NotaDebito)
            .Sum(v => v.Signo * v.Lineas.Sum(l => l.Cantidad)).ToString("N2");

        // Lo cobrado por método: los pagos de las facturas (las notas no llevan pagos). Un pago
        // en divisa se muestra con su monto en US$; uno en Bs, con su referencia a la tasa.
        PorMetodoPago.Clear();
        var pagos = enRango.SelectMany(v => v.Pagos.Select(p => (Venta: v, Pago: p))).ToList();
        foreach (var grupo in pagos.GroupBy(x => x.Pago.MetodoTexto)
                                   .OrderByDescending(g => g.Sum(x => x.Pago.MontoBs)))
            PorMetodoPago.Add(new ResumenMetodoPago(
                grupo.Key,
                grupo.Select(x => x.Venta.Id).Distinct().Count(),
                grupo.Sum(x => x.Pago.EsDivisa
                    ? x.Pago.MontoDivisa
                    : x.Venta.TasaCambioUsada > 0 ? x.Pago.MontoBs / x.Venta.TasaCambioUsada : 0m).ToString("N2"),
                grupo.Sum(x => x.Pago.MontoBs).ToString("N2")));

        GraficoPorDia = ConstruirGraficoPorDia(enRango);
        GraficoPorCategoria = ConstruirGraficoPorCategoria(enRango, articulosPorId);
    }

    /// <summary>Una barra horizontal por día; ver <see cref="Graficos.Barras"/>.</summary>
    private static PlotModel ConstruirGraficoPorDia(IReadOnlyList<Venta> ventas) =>
        Graficos.Barras("Ventas por día (Bs, neto de notas)",
            ventas.GroupBy(v => v.Fecha.Date)
                  .OrderBy(g => g.Key)
                  .Select(g => (g.Key.ToString("dd/MM"), (double)g.Sum(v => v.Signo * v.Total))));

    /// <summary>
    /// La categoría es texto libre en el artículo: se agrupa sin distinguir mayúsculas y un
    /// artículo sin categoría (o ya borrado) cae en "Sin categoría". Por renglón (una factura
    /// puede mezclar categorías), en Bs sin IVA y neto de notas de crédito. Los cargos de una
    /// nota de débito no son artículos y no entran.
    /// </summary>
    private static PlotModel ConstruirGraficoPorCategoria(IReadOnlyList<Venta> ventas,
                                                           IReadOnlyDictionary<int, Articulo> articulosPorId)
    {
        var porCategoria = ventas
            .SelectMany(v => v.Lineas.Where(l => l.ArticuloId is not null).Select(l => (Linea: l, v.Signo)))
            .GroupBy(x =>
                articulosPorId.TryGetValue(x.Linea.ArticuloId!.Value, out var a) && !string.IsNullOrWhiteSpace(a.Categoria)
                    ? a.Categoria.Trim()
                    : "Sin categoría",
                StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, (double)g.Sum(x => x.Signo * x.Linea.Monto)));

        return Graficos.Torta("Ventas por categoría (Bs)", porCategoria);
    }
}

/// <summary>Una fila de la tarjeta "Por método de pago" de Reportes · Ventas.</summary>
public sealed record ResumenMetodoPago(string Metodo, int Ventas, string TotalUsdTexto, string TotalBsTexto);
