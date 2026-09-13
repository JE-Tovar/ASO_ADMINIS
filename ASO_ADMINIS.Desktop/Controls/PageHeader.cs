using System.Windows;
using System.Windows.Controls;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Encabezado de una pantalla: título, descripción y, opcionalmente, acciones a la derecha
/// (el <c>Content</c> del control).
///
/// El mismo bloque de dieciocho líneas estaba copiado literalmente en veintiún vistas; el
/// comentario de <c>Styles/Theme.xaml</c> ya lo admitía sin llegar a resolverlo. Copiado veintiuna
/// veces significaba también veintiún sitios donde el tamaño del título, el color del subtítulo o
/// el margen inferior podían divergir, y divergían.
///
/// No lleva botón de "Volver al módulo": la ruta clicable de la barra superior lo sustituye.
/// </summary>
public class PageHeader : ContentControl
{
    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(PageHeader),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescripcionProperty =
        DependencyProperty.Register(nameof(Descripcion), typeof(string), typeof(PageHeader),
            new PropertyMetadata(string.Empty));

    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public string Descripcion
    {
        get => (string)GetValue(DescripcionProperty);
        set => SetValue(DescripcionProperty, value);
    }
}
