using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Filtro de listado colapsado en una caja desplegable, en vez de la fila de chips
/// (RadioButton + FilterChipStyle) que tenian las diecisiete pantallas de listado: con cinco o
/// seis estados por pantalla la fila se rompia en dos lineas en ventanas angostas.
///
/// Dispara <see cref="Command"/> con el valor del item elegido al cambiar la seleccion, igual que
/// hacia el Command/CommandParameter de cada RadioButton — así no hace falta tocar los
/// ViewModels, que ya exponen CambiarFiltroEstadoCommand como ICommand&lt;string&gt;. El valor sale
/// de <c>Tag</c> cuando el item lo trae (el rotulo no siempre coincide con el parametro que espera
/// el ViewModel, p. ej. "Últimos 7 días" -&gt; "Semana"), y si no de <c>Content</c>.
/// </summary>
public class FiltroDropdown : ComboBox
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(FiltroDropdown));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);

        var valor = SelectedItem is ComboBoxItem item
            ? (item.Tag as string ?? item.Content as string)
            : SelectedItem as string;

        if (valor is not null && (Command?.CanExecute(valor) ?? false))
            Command.Execute(valor);
    }
}
