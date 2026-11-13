using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Foco en la celda de cantidad de los renglones, para vender a granel con el teclado:
/// <list type="bullet">
/// <item><c>Enfoque.AlCargar="{Binding PideCantidad}"</c>: si es true cuando la celda aparece, toma
/// el foco y selecciona el texto (el renglón a granel recién agregado espera el peso).</item>
/// <item>Enter en esa caja devuelve el foco al <see cref="BuscadorArticulos"/> de la ventana para
/// seguir agregando, y se marca como manejado para que no dispare el botón por defecto
/// (Emitir / Registrar).</item>
/// </list>
/// Es comportamiento de vista (foco), no regla de negocio: por eso vive aquí y no en el ViewModel.
/// </summary>
public static class Enfoque
{
    public static readonly DependencyProperty AlCargarProperty =
        DependencyProperty.RegisterAttached("AlCargar", typeof(bool), typeof(Enfoque),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, AlCambiar));

    public static bool GetAlCargar(DependencyObject d) => (bool)d.GetValue(AlCargarProperty);
    public static void SetAlCargar(DependencyObject d, bool valor) => d.SetValue(AlCargarProperty, valor);

    private static readonly DependencyProperty ConectadoProperty =
        DependencyProperty.RegisterAttached("Conectado", typeof(bool), typeof(Enfoque), new PropertyMetadata(false));

    private static void AlCambiar(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox caja)
            return;

        if (!(bool)caja.GetValue(ConectadoProperty))
        {
            caja.SetValue(ConectadoProperty, true);
            caja.Loaded += (_, _) => { if (GetAlCargar(caja)) Enfocar(caja); };
            caja.PreviewKeyDown += AlTeclear;
        }

        if (e.NewValue is true && caja.IsLoaded)
            Enfocar(caja);
    }

    private static void Enfocar(TextBox caja) =>
        caja.Dispatcher.BeginInvoke(() =>
        {
            caja.Focus();
            caja.SelectAll();
        }, System.Windows.Threading.DispatcherPriority.Input);

    private static void AlTeclear(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox caja)
            return;

        // Ya se escribió la cantidad: se apaga el pedido. SetCurrentValue respeta el binding (de
        // dos vías por defecto), así que llega al PideCantidad del renglón.
        caja.SetCurrentValue(AlCargarProperty, false);

        if (Buscar<BuscadorArticulos>(Window.GetWindow(caja)) is { } buscador)
            buscador.Enfocar();

        e.Handled = true;
    }

    private static T? Buscar<T>(DependencyObject? raiz) where T : DependencyObject
    {
        if (raiz is null)
            return null;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var hijo = VisualTreeHelper.GetChild(raiz, i);
            if (hijo is T encontrado)
                return encontrado;
            if (Buscar<T>(hijo) is { } abajo)
                return abajo;
        }

        return null;
    }
}
