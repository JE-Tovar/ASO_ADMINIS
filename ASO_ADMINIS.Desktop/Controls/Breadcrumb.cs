using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// La ruta de la sección abierta, en la barra superior, con cada tramo anterior clicable.
///
/// No existía nada parecido. Saber dónde se estaba dependía de tres pistas repartidas —la barra
/// azul del menú lateral, el título que cada vista redibujaba por su cuenta y un botón "Volver al
/// módulo"— y el título acababa dicho tres veces: lo marcaba el menú, lo titulaba el resumen del
/// módulo y lo volvía a titular la pantalla. Con la ruta arriba, la pantalla ya no tiene que
/// repetirlo ni traer su propio botón de volver.
/// </summary>
public class Breadcrumb : Control
{
    public static readonly DependencyProperty SegmentosProperty =
        DependencyProperty.Register(nameof(Segmentos), typeof(IEnumerable), typeof(Breadcrumb),
            new PropertyMetadata(null));

    public IEnumerable? Segmentos
    {
        get => (IEnumerable?)GetValue(SegmentosProperty);
        set => SetValue(SegmentosProperty, value);
    }
}
