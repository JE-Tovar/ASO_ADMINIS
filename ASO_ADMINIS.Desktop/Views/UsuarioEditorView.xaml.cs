using System.Windows;
using System.Windows.Controls;
using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Desktop.Views;

public partial class UsuarioEditorView : UserControl
{
    public UsuarioEditorView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// <c>PasswordBox.Password</c> no se puede enlazar (WPF no lo expone como propiedad de
    /// dependencia, justamente para no dejar la contraseña colgando en el árbol visual), así
    /// que el único camino es empujarla al ViewModel desde aquí. Mismo criterio que en
    /// <see cref="LoginView"/>.
    /// </summary>
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UsuarioEditorViewModel vm)
            vm.PasswordNueva = PasswordBox.Password;
    }
}
