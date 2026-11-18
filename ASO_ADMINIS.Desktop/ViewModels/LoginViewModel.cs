using System.Linq;
using System.Threading.Tasks;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Lógica de presentación de la pantalla de inicio de sesión.
/// </summary>
public class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly ISesionActual _sesion;

    public LoginViewModel() : this(DataSourceFactory.CrearAuth(), SesionActual.Instancia) { }

    public LoginViewModel(IAuthService authService, ISesionActual sesion)
    {
        _authService = authService;
        _sesion = sesion;
    }

    private string _nombreUsuario = string.Empty;
    public string NombreUsuario
    {
        get => _nombreUsuario;
        set => SetProperty(ref _nombreUsuario, value);
    }

    /// <summary>
    /// Recordar el nombre de usuario, nunca la contraseña.
    ///
    /// La preferencia existía, pero solo se podía cambiar desde Configuración, que está detrás
    /// del inicio de sesión: para llegar a la casilla había que entrar antes. Aquí es donde se
    /// usa, así que aquí es donde tiene que poder cambiarse.
    /// </summary>
    public bool RecordarUsuario
    {
        get => Ajustes.Actual.RecordarUltimoUsuario;
        set
        {
            if (Ajustes.Actual.RecordarUltimoUsuario == value)
                return;

            Ajustes.Actual.RecordarUltimoUsuario = value;

            // Al apagarlo se borra el que ya estaba guardado: dejarlo escrito haria que la
            // casilla dijera una cosa y el disco otra.
            if (!value)
                Ajustes.Actual.UltimoUsuario = string.Empty;

            Ajustes.Guardar();
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// A qué base se está conectando, en una línea y sin credenciales.
    ///
    /// Antes no se decía en ninguna parte, y con la instalación pudiendo apuntar a un archivo
    /// LocalDB propio o a un SQL Server compartido (<c>appsettings.local.json</c>), entrar sin
    /// saber cuál de las dos es entrar a ciegas: los datos que se ven después dependen de eso.
    /// </summary>
    public static string OrigenDatos
    {
        get
        {
            try
            {
                var cadena = AppConfig.ConnectionString;

                var origen = cadena
                    .Split(';')
                    .Select(t => t.Trim())
                    .FirstOrDefault(t => t.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
                                      || t.StartsWith("Server=", StringComparison.OrdinalIgnoreCase));

                if (origen is null)
                    return string.Empty;

                var valor = origen[(origen.IndexOf('=') + 1)..].Trim();

                return valor.Contains("localdb", StringComparison.OrdinalIgnoreCase)
                    ? "Base local de esta máquina (LocalDB)"
                    : $"Base en {valor}";
            }
            catch
            {
                // El pie de la ventana es informativo: si la configuracion no se puede leer, el
                // fallo real saldra al intentar entrar, con su mensaje.
                return string.Empty;
            }
        }
    }

    private string _mensajeError = string.Empty;
    public string MensajeError
    {
        get => _mensajeError;
        set => SetProperty(ref _mensajeError, value);
    }

    /// <returns><c>true</c> si las credenciales son válidas e inicia la sesión.</returns>
    public bool IntentarIniciarSesion(string password)
    {
        try
        {
            var resultado = _authService.ValidarCredenciales(NombreUsuario, password);

            if (resultado is null)
            {
                Registro.Advertencia("Inicio de sesión rechazado para {Nombre}", NombreUsuario.Trim());
                MensajeError = "Usuario o contraseña incorrectos.";
                return false;
            }

            // Dentro del try porque iniciar sesión también toca la base: resuelve el núcleo
            // del usuario para fijar el ámbito.
            _sesion.IniciarSesion(resultado.Usuario, resultado.Ajustes);
            Registro.Info("Inicio de sesión de {Nombre} ({Rol})", resultado.Usuario.NombreUsuario, resultado.Usuario.RolTexto);

            // En segundo plano y en silencio: es la primera vez que esta app habla con
            // internet, y sin conexión no hay dónde avisar en pleno login. Administración
            // sigue mostrando la última tasa guardada mientras tanto.
            _ = ActualizarTasaCambioEnSegundoPlano();
            _ = RespaldoAutomaticoEnSegundoPlano(resultado.Usuario.NombreCompleto);
        }
        catch (SesionRechazadaException ex)
        {
            Registro.Advertencia("Sesión rechazada para {Nombre}: {Motivo}", NombreUsuario.Trim(), ex.Message);
            MensajeError = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            Registro.Error(ex, "Fallo al iniciar sesión (base de datos)");
            // Sin base de datos no hay login posible; decirlo aquí evita que el fallo
            // aparezca más tarde, a mitad de una navegación, como si fuera otra cosa.
            MensajeError = $"No se pudo conectar con la base de datos. {ex.Message}";
            return false;
        }

        MensajeError = string.Empty;
        return true;
    }

    /// <summary>
    /// El primer inicio de sesión del día (de cualquier PC) hace el respaldo, en segundo plano y
    /// sin avisar: SQL Server Express no trae el Agente que lo programaría. Si falla, queda en el
    /// historial de Administración · Respaldos y en el registro.
    /// </summary>
    private static async Task RespaldoAutomaticoEnSegundoPlano(string usuario)
    {
        try
        {
            var servicio = DataSourceFactory.CrearServicioRespaldos();
            await Task.Run(() => servicio.RespaldarSiToca(usuario));
        }
        catch (Exception ex)
        {
            Registro.Error(ex, "Falló el respaldo automático");
        }
    }

    private static async Task ActualizarTasaCambioEnSegundoPlano()
    {
        try
        {
            var servicio = new TasaCambioService(
                DataSourceFactory.CrearTasasCambio(), DataSourceFactory.CrearProveedorTasaCambio());

            await servicio.ActualizarDesdeApi();
        }
        catch (Exception ex)
        {
            // Sin internet o la fuente cayó: se sigue con la última tasa guardada. Quien la
            // necesite ve el botón "Actualizar" en Administración · Organización.
            Registro.Advertencia(ex, "No se pudo actualizar la tasa BCV");
        }
    }
}
