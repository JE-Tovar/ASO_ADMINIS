using System.Collections.ObjectModel;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Administración · Bitácora: quién cambió qué, cuándo y desde qué equipo. Solo lectura. Filtra
/// en la base (rango de fechas, texto, tipo de registro) y muestra como mucho
/// <see cref="Maximo"/> filas, las más nuevas.
/// </summary>
public sealed class BitacoraViewModel : ViewModelBase
{
    public const int Maximo = 1000;
    private const string Todas = "(todos)";

    private readonly IBitacoraDataSource _fuente;
    private readonly IServicioDialogo _dialogos;

    public BitacoraViewModel(IBitacoraDataSource fuente, IServicioDialogo dialogos, ISesionActual sesion)
    {
        _fuente = fuente;
        _dialogos = dialogos;
        PuedeVer = sesion.Puede(Permisos.Bitacora.Ver);
        BuscarCommand = new RelayCommand(Buscar, () => PuedeVer);
    }

    public bool PuedeVer { get; }

    public ICommand BuscarCommand { get; }

    public ObservableCollection<RegistroAuditoria> Registros { get; } = [];
    public ObservableCollection<CambioDeCampo> Cambios { get; } = [];
    public ObservableCollection<string> Entidades { get; } = [Todas];

    private DateTime? _desde = DateTime.Today.AddDays(-7);
    public DateTime? Desde
    {
        get => _desde;
        set => SetProperty(ref _desde, value);
    }

    private DateTime? _hasta = DateTime.Today;
    public DateTime? Hasta
    {
        get => _hasta;
        set => SetProperty(ref _hasta, value);
    }

    private string _texto = string.Empty;
    public string Texto
    {
        get => _texto;
        set => SetProperty(ref _texto, value);
    }

    private string _entidad = Todas;
    public string Entidad
    {
        get => _entidad;
        set { if (SetProperty(ref _entidad, value)) Buscar(); }
    }

    private RegistroAuditoria? _seleccionado;
    public RegistroAuditoria? Seleccionado
    {
        get => _seleccionado;
        set
        {
            if (!SetProperty(ref _seleccionado, value))
                return;

            Cambios.Clear();
            if (value is not null)
                foreach (var c in Auditoria.Leer(value.Detalle))
                    Cambios.Add(c);
        }
    }

    private string _estado = string.Empty;
    public string Estado
    {
        get => _estado;
        private set => SetProperty(ref _estado, value);
    }

    /// <summary>La primera vez que se abre la pestaña: no se consulta al construir Administración.</summary>
    public void CargarSiHaceFalta()
    {
        if (PuedeVer && Registros.Count == 0 && string.IsNullOrEmpty(Estado))
            Buscar();
    }

    public void Buscar()
    {
        if (!PuedeVer)
            return;

        var desde = Desde ?? DateTime.Today.AddDays(-7);
        var hasta = Hasta ?? DateTime.Today;
        if (hasta < desde)
            (desde, hasta) = (hasta, desde);

        try
        {
            var entidadesActuales = _fuente.Entidades();
            foreach (var e in entidadesActuales.Where(e => !Entidades.Contains(e)))
                Entidades.Add(e);

            var filas = _fuente.Consultar(desde, hasta, Texto, Entidad == Todas ? null : Entidad, Maximo);

            Registros.Clear();
            foreach (var r in filas)
                Registros.Add(r);
            Seleccionado = Registros.FirstOrDefault();

            Estado = filas.Count >= Maximo
                ? $"Se muestran los {Maximo} cambios más recientes del rango; acótalo para ver los anteriores."
                : $"{filas.Count} cambio(s) entre el {desde:dd/MM/yyyy} y el {hasta:dd/MM/yyyy}.";
        }
        catch (Exception ex)
        {
            Registro.Error(ex, "No se pudo consultar la bitácora");
            _dialogos.Informar("No se pudo consultar la bitácora", ex.Message);
        }
    }
}
