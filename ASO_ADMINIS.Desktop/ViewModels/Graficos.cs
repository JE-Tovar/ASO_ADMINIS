using System.Windows;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Fábrica de los gráficos de Reportes, para que todos se vean igual y quepan en su tarjeta.
///
/// Un <see cref="PlotModel"/> "pelado" pinta el texto en negro (no se ve en el tema oscuro), usa
/// un título grande sin aire, pone las etiquetas de la torta FUERA del círculo (se salen de una
/// tarjeta angosta) y no deja margen para la etiqueta de la barra más larga. Aquí se corrige
/// todo eso en un solo sitio.
///
/// Los colores salen del tema activo (<c>Colors.xaml</c>/<c>ColorsOscuro.xaml</c>) en el momento
/// de construir el modelo: como la pantalla se reconstruye al navegar, un cambio de tema se ve
/// al volver al reporte.
/// </summary>
public static class Graficos
{
    /// <summary>Máximo de porciones de una torta; el resto se junta en "Otros".</summary>
    private const int MaxPorciones = 6;

    public static PlotModel Modelo(string titulo)
    {
        var texto = Color("TextPrimaryColor", OxyColors.Black);
        var tenue = Color("TextMutedColor", OxyColors.Gray);

        return new PlotModel
        {
            Title = titulo,
            TitleFontSize = 13,
            TitleFontWeight = OxyPlot.FontWeights.Bold,
            TitleColor = texto,
            TitlePadding = 6,
            TextColor = tenue,
            DefaultFontSize = 11,
            PlotAreaBorderThickness = new OxyThickness(0),
            PlotAreaBorderColor = OxyColors.Transparent,
            Padding = new OxyThickness(4, 4, 12, 4),
            Background = OxyColors.Transparent
        };
    }

    /// <summary>
    /// Barras horizontales, una por categoría. Horizontales y no columnas porque
    /// <c>ColumnSeries</c> se retiró de OxyPlot 2.x; una barra por fila se lee igual de bien y la
    /// etiqueta de la categoría (fecha, mes) tiene todo el ancho que necesite.
    /// </summary>
    public static PlotModel Barras(string titulo, IEnumerable<(string Categoria, double Valor)> datos)
    {
        var modelo = Modelo(titulo);
        var categorias = EjeCategorias();
        var serie = new BarSeries
        {
            FillColor = Color("PrimaryButtonColor", OxyColors.SteelBlue),
            LabelPlacement = LabelPlacement.Outside,
            LabelFormatString = "{0:#,0}",
            TextColor = Color("TextSecondaryColor", OxyColors.Gray),
            StrokeThickness = 0
        };

        foreach (var (categoria, valor) in datos)
        {
            categorias.Labels.Add(categoria);
            serie.Items.Add(new BarItem { Value = valor });
        }

        modelo.Axes.Add(categorias);
        modelo.Axes.Add(EjeValores());
        modelo.Series.Add(serie);
        return modelo;
    }

    /// <summary>
    /// Barras apiladas: una fila por categoría y un tramo por serie, con leyenda abajo. Sin
    /// etiquetas de valor (en barras apiladas se pisan); el detalle está en la tabla.
    /// </summary>
    public static PlotModel BarrasApiladas(string titulo,
                                           IReadOnlyList<string> categorias,
                                           IReadOnlyList<(string Serie, IReadOnlyList<double> Valores)> series)
    {
        var modelo = Modelo(titulo);
        var eje = EjeCategorias();
        eje.Labels.AddRange(categorias);

        var paleta = Paleta();
        for (var i = 0; i < series.Count; i++)
        {
            var barra = new BarSeries
            {
                Title = series[i].Serie,
                IsStacked = true,
                FillColor = paleta[i % paleta.Count],
                StrokeThickness = 0
            };
            foreach (var valor in series[i].Valores)
                barra.Items.Add(new BarItem { Value = valor });
            modelo.Series.Add(barra);
        }

        modelo.Axes.Add(eje);
        modelo.Axes.Add(EjeValores(margenEtiqueta: false));
        modelo.Legends.Add(Leyenda(LegendPosition.BottomCenter, LegendOrientation.Horizontal));
        return modelo;
    }

    /// <summary>
    /// Torta con el porcentaje DENTRO de cada porción y los nombres en una leyenda a la derecha:
    /// así nada se sale de la tarjeta, por largos que sean los nombres. Se queda con las
    /// <see cref="MaxPorciones"/> más grandes y junta el resto en "Otros".
    /// </summary>
    public static PlotModel Torta(string titulo, IEnumerable<(string Nombre, double Valor)> datos)
    {
        var modelo = Modelo(titulo);
        var ordenados = datos.Where(d => d.Valor > 0).OrderByDescending(d => d.Valor).ToList();

        var visibles = ordenados.Count <= MaxPorciones
            ? ordenados
            : [.. ordenados.Take(MaxPorciones - 1), ("Otros", ordenados.Skip(MaxPorciones - 1).Sum(d => d.Valor))];

        var total = visibles.Sum(d => d.Valor);
        var paleta = Paleta();
        var serie = new PieSeries
        {
            StrokeThickness = 1.5,
            Stroke = Color("SurfaceColor", OxyColors.White),
            InsideLabelPosition = 0.6,
            InsideLabelFormat = "{2:0}%",
            InsideLabelColor = Color("OnAccentColor", OxyColors.White),
            AreInsideLabelsAngled = false,
            OutsideLabelFormat = null,
            TickHorizontalLength = 0,
            TickRadialLength = 0,
            Diameter = 0.92,
            StartAngle = -90
        };

        for (var i = 0; i < visibles.Count; i++)
        {
            var (nombre, valor) = visibles[i];
            var porcentaje = total > 0 ? valor / total * 100 : 0;
            serie.Slices.Add(new PieSlice($"{Recortar(nombre, 22)} ({porcentaje:0} %)", valor)
            {
                Fill = paleta[i % paleta.Count]
            });
        }

        modelo.Series.Add(serie);
        modelo.Legends.Add(Leyenda(LegendPosition.RightMiddle, LegendOrientation.Vertical));
        return modelo;
    }

    // ------------------------------------------------------------------ piezas

    private static CategoryAxis EjeCategorias() => new()
    {
        Position = AxisPosition.Left,
        TicklineColor = OxyColors.Transparent,
        AxislineStyle = LineStyle.None,
        GapWidth = 0.4
    };

    /// <summary>
    /// Eje de los montos. Con <paramref name="margenEtiqueta"/> deja un 20 % libre a la derecha
    /// para que la etiqueta "Outside" de la barra más larga no se corte contra el borde.
    /// </summary>
    private static LinearAxis EjeValores(bool margenEtiqueta = true) => new()
    {
        Position = AxisPosition.Bottom,
        MinimumPadding = 0,
        MaximumPadding = margenEtiqueta ? 0.2 : 0.05,
        AbsoluteMinimum = 0,
        StringFormat = "#,0",
        MajorGridlineStyle = LineStyle.Solid,
        MajorGridlineColor = Color("BorderColor", OxyColor.FromRgb(230, 230, 230)),
        TicklineColor = OxyColors.Transparent,
        AxislineStyle = LineStyle.None
    };

    private static Legend Leyenda(LegendPosition posicion, LegendOrientation orientacion) => new()
    {
        LegendPosition = posicion,
        LegendPlacement = LegendPlacement.Outside,
        LegendOrientation = orientacion,
        LegendBorderThickness = 0,
        LegendBackground = OxyColors.Transparent,
        LegendTextColor = Color("TextSecondaryColor", OxyColors.Gray),
        LegendFontSize = 11,
        LegendItemSpacing = 12,
        LegendPadding = 4,
        LegendSymbolLength = 12
    };

    /// <summary>
    /// Colores de las series: el ámbar de marca primero y después los de estado, que ya tienen
    /// su versión para cada tema y contraste entre sí.
    /// </summary>
    private static IReadOnlyList<OxyColor> Paleta() =>
    [
        Color("PrimaryButtonColor", OxyColors.Orange),
        Color("AccentBlueColor", OxyColors.SteelBlue),
        Color("SuccessColor", OxyColors.SeaGreen),
        Color("DangerColor", OxyColors.IndianRed),
        Color("InfoColor", OxyColors.CadetBlue),
        Color("WarningColor", OxyColors.Goldenrod),
        Color("TextMutedColor", OxyColors.Gray)
    ];

    private static OxyColor Color(string clave, OxyColor respaldo) =>
        Application.Current?.TryFindResource(clave) is System.Windows.Media.Color c
            ? OxyColor.FromArgb(c.A, c.R, c.G, c.B)
            : respaldo;

    private static string Recortar(string texto, int maximo) =>
        texto.Length <= maximo ? texto : texto[..(maximo - 1)] + "…";
}
