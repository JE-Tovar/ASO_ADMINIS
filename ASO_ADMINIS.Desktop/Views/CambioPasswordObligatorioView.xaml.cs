using System.Windows;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Desktop.Views;

/// <summary>
/// Se muestra justo después del login cuando el usuario tiene <c>DebeCambiarPassword</c>: entró
/// con una contraseña que puso un administrador. Sin cambiarla no se entra (Salir cierra la sesión).
/// Reutiliza la regla y el guardado de Configuración · Mi cuenta (<see cref="CuentaViewModel"/>).
/// </summary>
public partial class CambioPasswordObligatorioView : Window
{
    private readonly string _actual;
    private readonly CuentaViewModel _cuenta = new(DataSourceFactory.CrearUsuarios(), SesionActual.Instancia);

    public CambioPasswordObligatorioView(string passwordActual)
    {
        InitializeComponent();
        EscalaVentana.Aplicar(this);
        _actual = passwordActual;
        CampoNueva.Ayuda = PoliticaSeguridad.DescripcionPassword;
        Loaded += (_, _) => NuevaBox.Focus();
    }

    private void OnCambiar(object sender, RoutedEventArgs e)
    {
        if (_cuenta.CambiarPassword(_actual, NuevaBox.Password, ConfirmacionBox.Password))
        {
            Registro.Info("Cambio obligatorio de contraseña completado");
            DialogResult = true;
            return;
        }

        ErrorTexto.Text = _cuenta.ErrorPassword;
        ErrorTexto.Visibility = Visibility.Visible;
        NuevaBox.Clear();
        ConfirmacionBox.Clear();
        NuevaBox.Focus();
    }

    private void OnSalir(object sender, RoutedEventArgs e) => DialogResult = false;
}
