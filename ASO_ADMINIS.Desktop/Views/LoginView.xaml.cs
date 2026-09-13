using System.Windows;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Desktop.Views;

public partial class LoginView : Window
{
    public LoginViewModel ViewModel { get; } = new();

    public LoginView()
    {
        InitializeComponent();
        DataContext = ViewModel;
        EscalaVentana.Aplicar(this);

        // Se recuerda el nombre, nunca la contrasenna. Con el usuario ya puesto, el foco va
        // directo a lo unico que queda por escribir.
        if (Ajustes.Actual.RecordarUltimoUsuario && Ajustes.Actual.UltimoUsuario.Length > 0)
        {
            ViewModel.NombreUsuario = Ajustes.Actual.UltimoUsuario;
            PasswordBox.Focus();
            return;
        }

        UsuarioBox.Focus();
    }

    private void OnIniciarSesion(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IntentarIniciarSesion(PasswordBox.Password))
        {
            // Solo tras entrar de verdad: recordar un usuario que ni existe no ayudaria a nadie.
            if (Ajustes.Actual.RecordarUltimoUsuario)
            {
                Ajustes.Actual.UltimoUsuario = ViewModel.NombreUsuario.Trim();
                Ajustes.Guardar();
            }

            DialogResult = true;
            return;
        }

        PasswordBox.Clear();
        PasswordBox.Focus();
    }
}
