using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Doble clic (o Enter) sobre una fila de un <see cref="DataGrid"/> ejecuta un comando:
/// <c>controls:DobleClicFila.Comando="{Binding AbrirCommand}"</c>.
/// <list type="bullet">
/// <item>Solo cuenta el clic que cae en una fila: el doble clic en un encabezado (que ordena) o en
/// la barra de desplazamiento no abre nada.</item>
/// <item>Antes de ejecutar deja seleccionada la fila del clic, para que el comando, que trabaja
/// con <c>SelectedItem</c>, no actúe sobre otra.</item>
/// <item>Enter con una fila seleccionada hace lo mismo, salvo si el foco está en una caja de
/// texto dentro de la grilla (los renglones editables usan Enter para otra cosa).</item>
/// </list>
/// Es comportamiento de vista, como <see cref="Enfoque"/>: el qué se abre lo decide el ViewModel.
/// </summary>
public static class DobleClicFila
{
    public static readonly DependencyProperty ComandoProperty =
        DependencyProperty.RegisterAttached("Comando", typeof(ICommand), typeof(DobleClicFila),
            new PropertyMetadata(null, AlCambiar));

    public static ICommand? GetComando(DependencyObject d) => (ICommand?)d.GetValue(ComandoProperty);
    public static void SetComando(DependencyObject d, ICommand? valor) => d.SetValue(ComandoProperty, valor);

    private static void AlCambiar(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grilla)
            return;

        grilla.MouseDoubleClick -= AlDobleClic;
        grilla.PreviewKeyDown -= AlTeclear;

        if (e.NewValue is not null)
        {
            grilla.MouseDoubleClick += AlDobleClic;
            grilla.PreviewKeyDown += AlTeclear;
        }
    }

    private static void AlDobleClic(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grilla || e.ChangedButton != MouseButton.Left)
            return;

        var fila = Subir<DataGridRow>(e.OriginalSource as DependencyObject, hasta: grilla);
        if (fila is null || fila.Item == FilaNueva)
            return;

        grilla.SelectedItem = fila.Item;
        e.Handled = Ejecutar(grilla);
    }

    private static void AlTeclear(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not DataGrid grilla || grilla.SelectedItem is null)
            return;

        if (e.OriginalSource is TextBoxBase or ComboBox)
            return;

        e.Handled = Ejecutar(grilla);
    }

    private static bool Ejecutar(DataGrid grilla)
    {
        if (GetComando(grilla) is not { } comando || !comando.CanExecute(null))
            return false;

        comando.Execute(null);
        return true;
    }

    /// <summary>El marcador de "fila nueva" de un DataGrid editable; nunca es un registro.</summary>
    private static readonly object FilaNueva = System.Windows.Data.CollectionView.NewItemPlaceholder;

    /// <summary>
    /// Sube por el árbol visual desde el elemento del clic. Se detiene en la grilla: un
    /// encabezado o la barra de desplazamiento no tienen una fila por encima antes de llegar a ella.
    /// </summary>
    private static T? Subir<T>(DependencyObject? desde, DependencyObject hasta) where T : DependencyObject
    {
        var actual = desde;
        while (actual is not null && actual != hasta)
        {
            if (actual is T encontrado)
                return encontrado;

            actual = actual is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(actual)
                : LogicalTreeHelper.GetParent(actual);
        }

        return null;
    }
}
