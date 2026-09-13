using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Controls;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

public sealed class NavegacionEventArgs(Modulo modulo, Submodulo? submodulo) : EventArgs
{
    public Modulo Modulo { get; } = modulo;
    public Submodulo? Submodulo { get; } = submodulo;
}

public sealed class SubmoduloNavItem(Modulo modulo, Submodulo submodulo) : ViewModelBase
{
    public Modulo Modulo { get; } = modulo;
    public Submodulo Submodulo { get; } = submodulo;
    public string Nombre => Submodulo.Nombre;
    public string Icono => Submodulo.Icono;
    public string Descripcion => Submodulo.Descripcion;

    private bool _estaSeleccionado;
    public bool EstaSeleccionado
    {
        get => _estaSeleccionado;
        set => SetProperty(ref _estaSeleccionado, value);
    }
}

public sealed class ModuloNavItem : ViewModelBase
{
    public ModuloNavItem(Modulo modulo, ISesionActual sesion)
    {
        Modulo = modulo;
        Submodulos = [.. sesion.SubmodulosVisibles(modulo).Select(s => new SubmoduloNavItem(modulo, s))];
    }

    public Modulo Modulo { get; }
    public IReadOnlyList<SubmoduloNavItem> Submodulos { get; }
    public string Nombre => Modulo.Nombre;
    public string Icono => Modulo.Icono;
    public string Descripcion => Modulo.Descripcion;
    public bool TieneSubmodulos => Submodulos.Count > 0;

    private bool _estaSeleccionado;
    public bool EstaSeleccionado
    {
        get => _estaSeleccionado;
        set => SetProperty(ref _estaSeleccionado, value);
    }

    /// <summary>
    /// El módulo es el que se está viendo, aunque el contenido sea uno de sus submódulos.
    /// Sirve para dejarle una marca sutil al padre sin robarle la barra al submódulo activo.
    /// </summary>
    private bool _estaActivo;
    public bool EstaActivo
    {
        get => _estaActivo;
        set => SetProperty(ref _estaActivo, value);
    }

    private bool _estaExpandido;
    public bool EstaExpandido
    {
        get => _estaExpandido;
        set
        {
            if (SetProperty(ref _estaExpandido, value))
            {
                OnPropertyChanged(nameof(GlifoChevron));
                OnPropertyChanged(nameof(TextoChevron));
            }
        }
    }

    /// <summary>
    /// Cuántas cosas esperan en esta sección. Hoy solo lo usa Peticiones: la bandeja del
    /// administrador no decía cuántas solicitudes tenía sin resolver, así que había que entrar
    /// para saber si había trabajo.
    /// </summary>
    private int _contador;
    public int Contador
    {
        get => _contador;
        set
        {
            if (SetProperty(ref _contador, value))
                OnPropertyChanged(nameof(TieneContador));
        }
    }

    public bool TieneContador => Contador > 0;

    /// <summary>Etiqueta del chevron para lectores de pantalla y tooltip.</summary>
    public string TextoChevron => EstaExpandido ? $"Contraer {Nombre}" : $"Expandir {Nombre}";

    public string GlifoChevron => EstaExpandido ? Iconos.ChevronArriba : Iconos.ChevronAbajo;
}

/// <summary>
/// Un bloque del menú con su encabezado. Existe porque Administración quedaba intercalada entre
/// Inicio/Peticiones y los cinco módulos del negocio sin ningún separador: ocho entradas planas
/// seguidas, sin decir que las tres primeras son otra cosa que las cinco siguientes.
/// </summary>
public sealed class GrupoNav(string titulo, IReadOnlyList<ModuloNavItem> items)
{
    public string Titulo { get; } = titulo;
    public IReadOnlyList<ModuloNavItem> Items { get; } = items;
    public bool TieneItems => Items.Count > 0;
}

/// <summary>
/// Estado del menú lateral de dos niveles. Solo notifica la intención de navegar
/// (<see cref="NavegacionSolicitada"/>); quién muestra qué vista lo decide la ventana principal,
/// que a su vez confirma el estado visual llamando a <see cref="Sincronizar"/>.
/// </summary>
public sealed class SidebarViewModel : ViewModelBase
{
    public event EventHandler<NavegacionEventArgs>? NavegacionSolicitada;

    /// <summary>Lo que se pinta: dos bloques con encabezado.</summary>
    public IReadOnlyList<GrupoNav> Grupos { get; }

    /// <summary>Todos los ítems de <see cref="Grupos"/>, en plano.</summary>
    public IReadOnlyList<ModuloNavItem> Items { get; }

    /// <summary>
    /// El acceso a Configuración va anclado al pie del menú, fuera del scroll, así que no puede
    /// vivir en <see cref="Items"/>. Null si el rol no lo ve.
    /// </summary>
    public ModuloNavItem? Configuracion { get; }

    public bool VeConfiguracion => Configuracion is not null;

    /// <summary>
    /// Todo lo que se marca y se desmarca al navegar. Existe porque el ítem del pie está fuera
    /// de <see cref="Items"/>: si <see cref="Sincronizar"/> recorriera solo esa lista, entrar a
    /// Configuración no apagaría el módulo anterior y salir de ella no apagaría Configuración.
    /// </summary>
    private readonly IReadOnlyList<ModuloNavItem> _navegables;

    public ICommand SeleccionarModuloCommand { get; }
    public ICommand SeleccionarSubmoduloCommand { get; }
    public ICommand SeleccionarConfiguracionCommand { get; }
    public ICommand AlternarExpansionCommand { get; }

    public SidebarViewModel() : this(SesionActual.Instancia) { }

    public SidebarViewModel(ISesionActual sesion)
    {
        // El menu se arma una vez por ventana y la ventana se reconstruye al entrar, al salir
        // y al cambiar de nucleo, asi que aqui basta con filtrar al construir.
        List<ModuloNavItem> secciones =
            [.. ModuloCatalogo.Fijados.Where(sesion.Ve).Select(m => new ModuloNavItem(m, sesion))];

        List<ModuloNavItem> modulos =
            [.. sesion.ModulosVisibles().Select(m => new ModuloNavItem(m, sesion))];

        Grupos = [new GrupoNav("Secciones", secciones), new GrupoNav("Módulos", modulos)];
        Items = [.. secciones, .. modulos];

        Configuracion = sesion.Ve(ModuloCatalogo.Configuracion)
            ? new ModuloNavItem(ModuloCatalogo.Configuracion, sesion)
            : null;

        _navegables = Configuracion is null ? Items : [.. Items, Configuracion];

        // La caja del modulo tambien pliega: volver a pulsar el modulo que ya se esta viendo
        // cierra su rama. Antes la unica forma de cerrarla era acertarle al chevron, un blanco
        // de dieciseis pixeles al borde del menu, teniendo delante la fila entera.
        SeleccionarModuloCommand = new RelayCommand<ModuloNavItem>(item =>
        {
            if (item.EstaExpandido && item.EstaSeleccionado)
            {
                item.EstaExpandido = false;
                return;
            }

            NavegacionSolicitada?.Invoke(this, new NavegacionEventArgs(item.Modulo, null));
        });

        SeleccionarSubmoduloCommand = new RelayCommand<SubmoduloNavItem>(item =>
            NavegacionSolicitada?.Invoke(this, new NavegacionEventArgs(item.Modulo, item.Submodulo)));

        SeleccionarConfiguracionCommand = new RelayCommand(
            () => NavegacionSolicitada?.Invoke(
                this, new NavegacionEventArgs(ModuloCatalogo.Configuracion, null)),
            () => VeConfiguracion);

        // Plegar/desplegar es solo visual: no cambia de sección ni pide navegar.
        AlternarExpansionCommand = new RelayCommand<ModuloNavItem>(
            item => item.EstaExpandido = !item.EstaExpandido,
            item => item.TieneSubmodulos);

        if (secciones.FirstOrDefault(i => i.Modulo.Clave == ModuloCatalogo.Peticiones.Clave)
            is { } peticiones)
            _ = ContarPendientes(peticiones, sesion);
    }

    /// <summary>
    /// Pone el contador de la bandeja de peticiones, fuera del hilo de interfaz.
    ///
    /// Cuenta lo que ESTE usuario puede atender, no todo lo pendiente del nucleo: a un
    /// almacenista, que solo resuelve las peticiones de su dominio, un contador global le
    /// marcaria trabajo que no es suyo y que no puede quitar de en medio. Quien no resuelve
    /// sigue viendo el pendiente del nucleo, que es lo unico informativo para el.
    ///
    /// Va en segundo plano a propósito: el menú se construye durante el arranque de la ventana, y
    /// una consulta a SQL Server ahí dejaría la aplicación en blanco hasta que respondiera. El
    /// contador es informativo, así que si la consulta falla se queda en cero y el menú no se
    /// entera — no hay dónde informar de un error en un adorno, y desde luego no a costa de
    /// impedir que se abra la ventana.
    /// </summary>
    private static async Task ContarPendientes(ModuloNavItem peticiones, ISesionActual sesion)
    {
        try
        {
            var fuente = DataSourceFactory.CrearPeticiones();
            var resuelve = sesion.Puede(Permisos.Peticiones.Resolver);
            var propioId = sesion.UsuarioActual?.Id;

            peticiones.Contador = await Task.Run(
                () => fuente.GetAll().Count(p => p.EstaPendiente
                    && (!resuelve
                        || (sesion.Puede(p.Permiso) && p.SolicitadoPorId != propioId))));
        }
        catch
        {
            // Sin contador. Ver el resumen de la propia bandeja.
        }
    }

    /// <summary>
    /// Refleja en el menú la sección que se está mostrando, venga del propio menú o de una
    /// tarjeta del dashboard. Al entrar a un módulo se despliega su lista de submódulos.
    /// </summary>
    public void Sincronizar(Modulo modulo, Submodulo? submodulo)
    {
        foreach (var item in _navegables)
        {
            var esModuloActivo = item.Modulo.Clave == modulo.Clave;

            item.EstaActivo = esModuloActivo;
            item.EstaSeleccionado = esModuloActivo && submodulo is null;
            item.EstaExpandido = esModuloActivo && item.TieneSubmodulos;

            foreach (var sub in item.Submodulos)
                sub.EstaSeleccionado = esModuloActivo && sub.Submodulo.Clave == submodulo?.Clave;
        }
    }
}
