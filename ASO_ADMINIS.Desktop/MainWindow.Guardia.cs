using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.Views;

namespace ASO_ADMINIS.Desktop;

/// <summary>
/// Lo que vigila la sesión mientras la ventana está abierta:
/// <list type="bullet">
///   <item><b>Inactividad:</b> tras <c>Organizacion.MinutosInactividad</c> sin teclado ni ratón se
///         tapa todo con <see cref="SesionBloqueadaView"/>.</item>
///   <item><b>Permisos en caliente:</b> cada minuto se relee el usuario. Si cambió su rol, sus
///         permisos o su caja, se aplica y se redibuja el menú; si lo desactivaron, se cierra la
///         sesión.</item>
/// </list>
/// Redibujar o cerrar con un editor modal abierto le quitaría la ventana de debajo a ese editor,
/// así que se espera a que no haya ninguno (salvo al revocar, que no espera: se cierran).
/// </summary>
public partial class MainWindow
{
    private readonly DispatcherTimer _relojInactividad = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly DispatcherTimer _relojPermisos = new() { Interval = PoliticaSeguridad.IntervaloRefrescoPermisos };
    private DateTime _ultimaActividad = DateTime.Now;
    private bool _bloqueada;
    private bool _redibujarPendiente;
    private bool _leyendoPermisos;
    private bool _saliendo;

    private void IniciarGuardia()
    {
        InputManager.Current.PreProcessInput += AlHaberEntrada;
        _relojInactividad.Tick += (_, _) => RevisarInactividad();
        _relojPermisos.Tick += async (_, _) => await RevisarPermisos();
        _relojInactividad.Start();
        _relojPermisos.Start();
    }

    private void DetenerGuardia()
    {
        InputManager.Current.PreProcessInput -= AlHaberEntrada;
        _relojInactividad.Stop();
        _relojPermisos.Stop();
    }

    private void AlHaberEntrada(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is KeyboardEventArgs or MouseButtonEventArgs or MouseWheelEventArgs
            || (e.StagingItem.Input is MouseEventArgs && e.StagingItem.Input.RoutedEvent == Mouse.MouseMoveEvent))
            _ultimaActividad = DateTime.Now;
    }

    // ---------------------------------------------------------------- Inactividad

    private void RevisarInactividad()
    {
        var minutos = Ambito.Actual?.MinutosInactividad ?? 0;
        if (_bloqueada || _saliendo || minutos <= 0 || SesionActual.Instancia.UsuarioActual is not { } usuario)
            return;

        if (DateTime.Now - _ultimaActividad < TimeSpan.FromMinutes(minutos))
            return;

        _bloqueada = true;
        Registro.Info("Sesión bloqueada por {Minutos} minutos de inactividad", minutos);
        try
        {
            var bloqueo = new SesionBloqueadaView(usuario.NombreUsuario, usuario.NombreCompleto, minutos)
            {
                Owner = VentanaDeArriba()
            };

            if (bloqueo.ShowDialog() == true)
            {
                _ultimaActividad = DateTime.Now;
                return;
            }

            SalirDeLaSesion(null);
        }
        finally
        {
            _bloqueada = false;
        }
    }

    // ---------------------------------------------------------------- Permisos

    private async Task RevisarPermisos()
    {
        if (_leyendoPermisos || _bloqueada || _saliendo)
            return;

        _leyendoPermisos = true;
        try
        {
            var sesion = SesionActual.Instancia;
            var lectura = await Task.Run(sesion.Leer);
            var (cambio, motivo) = sesion.Aplicar(lectura);

            switch (cambio)
            {
                case SesionActual.Cambio.Revocada:
                    Registro.Advertencia("Sesión revocada: {Motivo}", motivo);
                    SalirDeLaSesion(motivo);
                    return;

                case SesionActual.Cambio.Actualizada:
                    _redibujarPendiente = true;
                    break;
            }

            if (_redibujarPendiente && !HayModalAbierto())
            {
                _redibujarPendiente = false;
                Redibujar();
            }
        }
        catch (Exception ex)
        {
            // Sin base no se puede comprobar nada; se reintenta en el próximo minuto.
            Registro.Advertencia(ex, "No se pudo refrescar los permisos de la sesión");
        }
        finally
        {
            _leyendoPermisos = false;
        }
    }

    /// <summary>Otra ventana con el mismo usuario y los permisos nuevos: menú, cabecera y pantalla.</summary>
    private void Redibujar()
    {
        var nueva = new MainWindow();
        Application.Current.MainWindow = nueva;
        nueva.Show();
        Close();

        MessageBox.Show(nueva,
            "Un administrador cambió tus permisos o tu caja. El menú ya muestra lo que puedes hacer ahora.",
            "ASO Adminis", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// Cierra la sesión desde la guardia: primero los editores abiertos (lo que tuvieran sin
    /// guardar se pierde, como al cerrar la sesión a mano) y, ya fuera de sus bucles modales, el
    /// mismo camino que el botón "Cerrar sesión".
    /// </summary>
    private void SalirDeLaSesion(string? motivo)
    {
        if (_saliendo)
            return;
        _saliendo = true;

        foreach (var ventana in Application.Current.Windows.OfType<Window>().Where(w => w != this).ToList())
            ventana.Close();

        Dispatcher.BeginInvoke(() =>
        {
            if (motivo is not null)
                MessageBox.Show(this, $"{motivo}\n\nSe cerró la sesión.", "ASO Adminis",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            CerrarSesionYVolverAlLogin();
        });
    }

    private bool HayModalAbierto() =>
        Application.Current.Windows.OfType<Window>().Any(w => w != this && w.IsVisible);

    private Window VentanaDeArriba() =>
        Application.Current.Windows.OfType<Window>().LastOrDefault(w => w.IsActive || w.IsVisible) ?? this;
}
