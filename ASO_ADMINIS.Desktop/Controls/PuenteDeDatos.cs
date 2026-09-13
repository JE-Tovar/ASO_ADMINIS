using System.Windows;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Puente para llegar al <c>DataContext</c> de la pantalla desde donde no hay árbol visual.
///
/// Las columnas de un <c>DataGrid</c> no son elementos del árbol: no heredan el
/// <c>DataContext</c>, así que un <c>Binding</c> puesto en una columna —su <c>Visibility</c>, o
/// el <c>Command</c> de un botón de su plantilla— no encuentra contra qué resolverse y falla en
/// silencio. Al declarar este puente en los recursos del control, sí hereda el
/// <c>DataContext</c> (los recursos se resuelven por el árbol lógico), y la columna llega a él
/// como <c>StaticResource</c>.
///
/// Es un <see cref="Freezable"/> y no un objeto cualquiera porque es lo que le da esa herencia.
///
/// <code>
/// &lt;UserControl.Resources&gt;
///   &lt;controls:PuenteDeDatos x:Key="Puente" Datos="{Binding}" /&gt;
/// &lt;/UserControl.Resources&gt;
/// ...
/// Visibility="{Binding Datos.MuestraPrecios, Source={StaticResource Puente},
///                      Converter={StaticResource BoolToVis}}"
/// </code>
/// </summary>
public sealed class PuenteDeDatos : Freezable
{
    public static readonly DependencyProperty DatosProperty =
        DependencyProperty.Register(nameof(Datos), typeof(object), typeof(PuenteDeDatos),
            new UIPropertyMetadata(null));

    public object? Datos
    {
        get => GetValue(DatosProperty);
        set => SetValue(DatosProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new PuenteDeDatos();
}
