using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Desktop.Views;

/// <summary>
/// El code-behind solo atiende el teclado y el clic de las sugerencias de cliente; qué se sugiere
/// y qué se carga es de <see cref="VentaEditorViewModel"/>. Mismo esquema que
/// <c>Controls/BuscadorArticulos</c>.
/// </summary>
public partial class VentaEditorView : UserControl
{
    public VentaEditorView()
    {
        InitializeComponent();

        ListaClientes.SelectionChanged += (_, _) =>
        {
            if (ListaClientes.SelectedItem is { } item)
                ListaClientes.ScrollIntoView(item);
        };
    }

    private VentaEditorViewModel? Vm => DataContext as VentaEditorViewModel;

    private void CajaRif_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Vm is not { } vm)
            return;

        switch (e.Key)
        {
            // Handled siempre: si no, Enter llegaría al botón "Emitir factura" (IsDefault).
            case Key.Enter:
                vm.ConfirmarClienteCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Down:
                vm.BajarClienteCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Up:
                vm.SubirClienteCommand.Execute(null);
                e.Handled = true;
                break;

            // Con la lista abierta, Esc solo la cierra; cerrada, sigue cancelando la ventana.
            case Key.Escape when vm.MostrarSugerencias:
                vm.CerrarSugerenciasCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void ListaClientes_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var origen = e.OriginalSource as DependencyObject;
        while (origen is not null and not ListBoxItem)
            origen = VisualTreeHelper.GetParent(origen);

        if (origen is ListBoxItem { DataContext: Cliente cliente } && Vm is { } vm)
        {
            vm.ElegirClienteCommand.Execute(cliente);
            CajaRif.Focus();
            e.Handled = true;
        }
    }
}
