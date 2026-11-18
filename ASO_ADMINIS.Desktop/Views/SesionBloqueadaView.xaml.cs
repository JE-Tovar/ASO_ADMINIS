using System.ComponentModel;
using System.Windows;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.Views;

/// <summary>
/// Tras unos minutos sin uso (<c>Organizacion.MinutosInactividad</c>) la ventana principal queda
/// tapada por esta: para seguir hay que escribir la contraseña del usuario de la sesión, o salir
/// a "Cambiar de usuario". La contraseña pasa por <see cref="IAuthService"/>, así que los intentos
/// fallidos cuentan para el bloqueo de la cuenta igual que en el login.
///
/// DialogResult true = desbloqueada; false = cerrar la sesión. No se cierra con la X: cerrarla
/// sin decidir dejaría la sesión abierta detrás.
/// </summary>
public partial class SesionBloqueadaView : Window
{
    private readonly IAuthService _auth = DataSourceFactory.CrearAuth();
    private readonly string _nombreUsuario;
    private bool _decidido;

    public SesionBloqueadaView(string nombreUsuario, string nombreCompleto, int minutos)
    {
        InitializeComponent();
        EscalaVentana.Aplicar(this);
        _nombreUsuario = nombreUsuario;
        Explicacion.Text = $"Pasaron {minutos} minutos sin uso. Escribe la contraseña de {nombreCompleto} para seguir, " +
                           "o cambia de usuario.";
        Loaded += (_, _) => PasswordBox.Focus();
        Closing += OnClosing;
    }

    private void OnDesbloquear(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_auth.ValidarCredenciales(_nombreUsuario, PasswordBox.Password) is not null)
            {
                Registro.Info("Sesión desbloqueada tras inactividad");
                Decidir(true);
                return;
            }

            Mostrar("Contraseña incorrecta.");
        }
        catch (SesionRechazadaException ex)
        {
            // La cuenta quedó bloqueada por intentos fallidos: aquí ya no hay nada que hacer.
            MessageBox.Show(this, ex.Message, "ASO Adminis", MessageBoxButton.OK, MessageBoxImage.Warning);
            Decidir(false);
        }
        catch (Exception ex)
        {
            Registro.Error(ex, "No se pudo validar la contraseña al desbloquear");
            Mostrar($"No se pudo conectar con la base de datos. {ex.Message}");
        }
    }

    private void OnCambiarUsuario(object sender, RoutedEventArgs e) => Decidir(false);

    private void Decidir(bool desbloqueada)
    {
        _decidido = true;
        DialogResult = desbloqueada;
    }

    private void Mostrar(string error)
    {
        ErrorTexto.Text = error;
        ErrorTexto.Visibility = Visibility.Visible;
        PasswordBox.Clear();
        PasswordBox.Focus();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_decidido)
            e.Cancel = true;
    }
}
