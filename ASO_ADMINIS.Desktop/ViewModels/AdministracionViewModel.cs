using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Administración del sistema: los usuarios con sus permisos, y la ficha de la propia
/// organización.
///
/// Los permisos NO son una pestaña aparte. Eran un CRUD de ajustes sueltos, y obligaba a trabajar
/// al revés: para saber qué podía hacer alguien había que recordar qué trae su rol y cruzarlo con
/// filas dispersas. Ahora al seleccionar un usuario se ven todos sus permisos al lado
/// (<see cref="PermisosDeUsuarioViewModel"/>), que es como se piensa la pregunta.
/// </summary>
public sealed class AdministracionViewModel : PantallaViewModelBase
{
    public const string VistaOrganizacion = "Organizacion";
    public const string VistaUsuarios = "Usuarios";
    public const string VistaCajas = "Cajas";
    public const string VistaBitacora = "Bitacora";
    public const string VistaRespaldos = "Respaldos";
    public const string VistaContingencia = "Contingencia";

    private readonly IServicioDialogo _dialogos;

    /// <summary>
    /// Para poder deshacer un cambio de selección: si el usuario declina descartar sus cambios,
    /// hay que devolver la grilla a donde estaba, y para entonces ya se movió.
    /// </summary>
    private Models.Usuario? _usuarioMostrado;
    private bool _restaurandoSeleccion;

    public AdministracionViewModel(Modulo modulo)
        : this(modulo, new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private AdministracionViewModel(Modulo modulo, IServicioDialogo dialogos, ISesionActual sesion)
        : base(modulo)
    {
        _dialogos = dialogos;

        var tasaCambio = new TasaCambioService(
            DataSourceFactory.CrearTasasCambio(), DataSourceFactory.CrearProveedorTasaCambio());

        Organizacion = new DatosOrganizacionViewModel(
            DataSourceFactory.CrearOrganizaciones(), dialogos, sesion, tasaCambio);
        Usuarios = new UsuariosCrudViewModel(DataSourceFactory.CrearUsuarios(), dialogos, sesion,
                                             DataSourceFactory.CrearCajas());
        Cajas = new CajasCrudViewModel(DataSourceFactory.CrearCajas(), dialogos, sesion);
        Bitacora = new BitacoraViewModel(DataSourceFactory.CrearBitacora(), dialogos, sesion);
        Respaldos = new RespaldosViewModel(DataSourceFactory.CrearServicioRespaldos(), dialogos, sesion);
        Contingencia = new RangosContingenciaCrudViewModel(DataSourceFactory.CrearRangosContingencia(), dialogos, sesion);
        PermisosDeUsuario = new PermisosDeUsuarioViewModel(
            DataSourceFactory.CrearPermisosUsuario(), dialogos, sesion);

        Usuarios.PropertyChanged += OnUsuarioSeleccionado;

        CambiarVistaCommand = new RelayCommand<string>(vista => VistaActual = vista);
    }

    public DatosOrganizacionViewModel Organizacion { get; }
    public UsuariosCrudViewModel Usuarios { get; }
    public CajasCrudViewModel Cajas { get; }
    public BitacoraViewModel Bitacora { get; }
    public RespaldosViewModel Respaldos { get; }
    public RangosContingenciaCrudViewModel Contingencia { get; }
    public PermisosDeUsuarioViewModel PermisosDeUsuario { get; }

    /// <summary>
    /// El panel de permisos sigue a la selección del padrón. Si hay cambios sin guardar se
    /// pregunta antes de descartarlos; al declinar se devuelve la selección a su sitio.
    /// </summary>
    private void OnUsuarioSeleccionado(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Usuarios.SelectedItem) || _restaurandoSeleccion)
            return;

        var nuevo = Usuarios.SelectedItem;
        if (ReferenceEquals(nuevo, _usuarioMostrado))
            return;

        if (PermisosDeUsuario.HayCambios
            && !_dialogos.Confirmar(
                "Cambios sin guardar",
                $"Hay permisos modificados de {PermisosDeUsuario.Usuario?.NombreUsuario} que no se han guardado. ¿Descartarlos?"))
        {
            _restaurandoSeleccion = true;
            Usuarios.SelectedItem = _usuarioMostrado;
            _restaurandoSeleccion = false;
            return;
        }

        _usuarioMostrado = nuevo;
        PermisosDeUsuario.Cargar(nuevo);
    }

    /// <summary>
    /// Relee el padrón de usuarios y vuelve a apuntar el panel de permisos al mismo usuario.
    ///
    /// Va con la guarda <c>_restaurandoSeleccion</c> puesta porque la recarga trae instancias
    /// NUEVAS: sin ella, <see cref="OnUsuarioSeleccionado"/> creería que se cambió de fila y
    /// ofrecería descartar los permisos que se estén editando. La ficha de la organización no se
    /// toca — es un formulario de una sola fila y recargarlo pisaría lo que se está escribiendo.
    /// </summary>
    public override void Recargar()
    {
        var idMostrado = _usuarioMostrado?.Id;

        Cajas.Recargar();
        Contingencia.Recargar();

        _restaurandoSeleccion = true;
        Usuarios.Recargar();
        _restaurandoSeleccion = false;

        _usuarioMostrado = idMostrado is { } id
            ? Usuarios.Items.FirstOrDefault(u => u.Id == id)
            : null;

        // Un panel con cambios a medio hacer no se pisa; se recarga cuando ya se guardaron.
        if (!PermisosDeUsuario.HayCambios)
            PermisosDeUsuario.Cargar(_usuarioMostrado);
    }

    private string _vistaActual = VistaUsuarios;
    public string VistaActual
    {
        get => _vistaActual;
        set
        {
            // Notifica todas: enumerar aqui un OnPropertyChanged por cada Mostrar… es
            // la lista que se queda corta el dia que se agrega un padron mas.
            if (SetProperty(ref _vistaActual, value))
                OnTodasLasPropiedadesCambiaron();
        }
    }

    public bool MostrarOrganizacion => VistaActual == VistaOrganizacion;
    public bool MostrarUsuarios => VistaActual == VistaUsuarios;
    public bool MostrarCajas => VistaActual == VistaCajas;
    public bool MostrarBitacora => VistaActual == VistaBitacora;
    public bool MostrarRespaldos => VistaActual == VistaRespaldos;
    public bool MostrarContingencia => VistaActual == VistaContingencia;

    /// <summary>
    /// Las dos pestanas, enlazadas en DOS VIAS al IsChecked de su boton. Antes la seleccion
    /// viajaba solo de la vista al ViewModel por Command, con IsChecked="True" a fuego en la
    /// primera: si algo cambiaba VistaActual desde el codigo, los botones no se enteraban.
    ///
    /// El setter solo actua al marcar: al desmarcar ya hay otro boton del grupo encendiendose.
    /// </summary>
    public bool EsUsuarios
    {
        get => MostrarUsuarios;
        set { if (value) VistaActual = VistaUsuarios; }
    }

    public bool EsContingencia
    {
        get => MostrarContingencia;
        set { if (value) VistaActual = VistaContingencia; }
    }

    public bool EsRespaldos
    {
        get => MostrarRespaldos;
        set
        {
            if (!value)
                return;
            VistaActual = VistaRespaldos;
            Respaldos.CargarSiHaceFalta();
        }
    }

    public bool EsBitacora
    {
        get => MostrarBitacora;
        set
        {
            if (!value)
                return;
            VistaActual = VistaBitacora;
            Bitacora.CargarSiHaceFalta();
        }
    }

    public bool EsCajas
    {
        get => MostrarCajas;
        set { if (value) VistaActual = VistaCajas; }
    }

    public bool EsOrganizacion
    {
        get => MostrarOrganizacion;
        set { if (value) VistaActual = VistaOrganizacion; }
    }

    public ICommand CambiarVistaCommand { get; }
}
