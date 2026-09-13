using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Desktop.Views;

/// <summary>
/// Code-behind mínimo, y solo por las contraseñas: el <c>PasswordBox</c> de WPF no expone su
/// contenido como propiedad enlazable, así que hay que leerlo aquí y pasárselo al ViewModel.
/// Es la misma excepción que ya se hace en <see cref="LoginView"/>.
/// </summary>
public partial class ConfiguracionView : UserControl
{
    public ConfiguracionView() => InitializeComponent();

    private void OnCambiarPassword(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ConfiguracionViewModel vm)
            return;

        if (!vm.Cuenta.CambiarPassword(PasswordActual.Password,
                                       PasswordNueva.Password,
                                       PasswordConfirmacion.Password))
        {
            return;
        }

        // Solo se limpian si el cambio salió bien: si falló, quien lo escribió quiere corregir
        // un campo, no volver a escribir los tres.
        PasswordActual.Clear();
        PasswordNueva.Clear();
        PasswordConfirmacion.Clear();
        PasswordActual.Focus();
    }

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OnCambiarPassword(sender, e);
    }
}
