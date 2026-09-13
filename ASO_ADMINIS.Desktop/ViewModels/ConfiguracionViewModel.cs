using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Configuración: lo que se ajusta desde dentro de la aplicación y no es dato del negocio.
///
/// Tres pestañas con tres alcances distintos, y esa es la razón de que estén separadas:
/// <b>Apariencia</b> es de esta máquina, <b>Mi cuenta</b> es de quien está dentro, y
/// <b>Aplicación</b> cambia cómo se comporta el sistema para todos los que lo usen aquí — por
/// eso es la única con permiso propio.
///
/// No se metió en Administración a propósito: allí se administra a los demás, aquí cada quien
/// ajusta lo suyo, y el remesero entra en la segunda pero no en la primera.
/// </summary>
public sealed class ConfiguracionViewModel : PantallaViewModelBase
{
    public const string VistaApariencia = "Apariencia";
    public const string VistaCuenta = "Cuenta";
    public const string VistaAplicacion = "Aplicacion";

    public ConfiguracionViewModel(Modulo modulo)
        : this(modulo, new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private ConfiguracionViewModel(Modulo modulo, IServicioDialogo dialogos, ISesionActual sesion)
        : base(modulo)
    {
        Apariencia = new AparienciaViewModel();
        Cuenta = new CuentaViewModel(DataSourceFactory.CrearUsuarios(), sesion);
        Aplicacion = new PreferenciasAppViewModel();

    }

    public AparienciaViewModel Apariencia { get; }
    public CuentaViewModel Cuenta { get; }
    public PreferenciasAppViewModel Aplicacion { get; }

    private string _vistaActual = VistaApariencia;
    public string VistaActual
    {
        get => _vistaActual;
        set
        {
            // Notifica todas: enumerar aqui un OnPropertyChanged por cada Mostrar… es
            // la lista que se queda corta el dia que se agrega una pestanna mas.
            if (SetProperty(ref _vistaActual, value))
                OnTodasLasPropiedadesCambiaron();
        }
    }

    public bool MostrarApariencia => VistaActual == VistaApariencia;
    public bool MostrarCuenta => VistaActual == VistaCuenta;
    public bool MostrarAplicacion => VistaActual == VistaAplicacion;

    /// <summary>
    /// Las tres pestanas, enlazadas en DOS VIAS al IsChecked de su boton.
    ///
    /// Antes iban de una sola: la seleccion viajaba de la vista al ViewModel por Command, y el
    /// primer boton llevaba IsChecked="True" escrito a fuego. Si algo cambiaba VistaActual desde
    /// el codigo, los botones seguian marcando la pestana anterior. Escrito asi, el estado vive
    /// en un solo sitio y los dos lados lo leen de ahi.
    ///
    /// El setter solo actua al marcar: al desmarcar ya hay otro boton del grupo encendiendose, y
    /// atender los dos avisos apagaria la pestana recien elegida.
    /// </summary>
    public bool EsApariencia
    {
        get => MostrarApariencia;
        set { if (value) VistaActual = VistaApariencia; }
    }

    public bool EsCuenta
    {
        get => MostrarCuenta;
        set { if (value) VistaActual = VistaCuenta; }
    }

    public bool EsAplicacion
    {
        get => MostrarAplicacion;
        set { if (value) VistaActual = VistaAplicacion; }
    }
}
