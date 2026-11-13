using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Vista de <see cref="BuscadorArticulosViewModel"/>. El code-behind solo atiende teclado, foco y
/// clic; qué se busca y qué se elige es del ViewModel.
/// </summary>
public partial class BuscadorArticulos : UserControl
{
    public BuscadorArticulos()
    {
        InitializeComponent();

        // Al abrir el documento se empieza buscando: el cursor ya está en la caja.
        Loaded += (_, _) => Dispatcher.BeginInvoke(() => Caja.Focus(),
                                                   System.Windows.Threading.DispatcherPriority.Input);

        Resultados.SelectionChanged += (_, _) =>
        {
            if (Resultados.SelectedItem is { } item)
                Resultados.ScrollIntoView(item);
        };
    }

    private BuscadorArticulosViewModel? Vm => DataContext as BuscadorArticulosViewModel;

    /// <summary>Devuelve el cursor a la caja (lo usa <see cref="Enfoque"/> tras escribir una cantidad).</summary>
    public void Enfocar()
    {
        Caja.Focus();
        Caja.SelectAll();
    }

    private void Caja_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Vm is not { } vm)
            return;

        switch (e.Key)
        {
            // Handled siempre: si no, Enter llegaría al botón IsDefault de la ventana y emitiría
            // la factura (o registraría el documento) en vez de agregar el producto.
            case Key.Enter:
                vm.ConfirmarCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Down:
                vm.BajarCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Up:
                vm.SubirCommand.Execute(null);
                e.Handled = true;
                break;

            // Con la lista abierta, Esc solo la cierra; con la lista cerrada, sigue cancelando la
            // ventana como siempre.
            case Key.Escape when vm.MostrarResultados:
                vm.CerrarCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void Caja_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (Vm is { HayResultados: true } vm && !string.IsNullOrWhiteSpace(vm.Texto))
            vm.MostrarResultados = true;
    }

    private void Resultados_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Un clic en una fila la agrega. Se busca el contenedor bajo el ratón porque los items no
        // son enfocables (el foco se queda en la caja para seguir escribiendo).
        var origen = e.OriginalSource as DependencyObject;
        while (origen is not null and not ListBoxItem)
            origen = VisualTreeHelper.GetParent(origen);

        if (origen is ListBoxItem { DataContext: Articulo articulo } && Vm is { } vm)
        {
            vm.ElegirCommand.Execute(articulo);
            Caja.Focus();
            e.Handled = true;
        }
    }
}
