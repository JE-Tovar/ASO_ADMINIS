using System.Windows;
using System.Windows.Controls;
using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Torta de Reportes con la leyenda al lado, dentro de la misma tarjeta: cada color con su
/// nombre, % y monto. Ver <see cref="Graficos.Torta"/>.
/// </summary>
public partial class TortaConLeyenda : UserControl
{
    public static readonly DependencyProperty GraficoProperty =
        DependencyProperty.Register(nameof(Grafico), typeof(GraficoTorta), typeof(TortaConLeyenda));

    public GraficoTorta? Grafico
    {
        get => (GraficoTorta?)GetValue(GraficoProperty);
        set => SetValue(GraficoProperty, value);
    }

    public TortaConLeyenda() => InitializeComponent();
}
