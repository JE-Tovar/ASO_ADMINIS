using System.Collections.ObjectModel;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Administración · Respaldos: el historial (nativo y portable), "Respaldar ahora" y dónde quedan
/// los archivos. El respaldo corre en segundo plano: puede tardar minutos con una base grande.
/// </summary>
public sealed class RespaldosViewModel : ViewModelBase
{
    private readonly RespaldoService _servicio;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesion;

    public RespaldosViewModel(RespaldoService servicio, IServicioDialogo dialogos, ISesionActual sesion)
    {
        _servicio = servicio;
        _dialogos = dialogos;
        _sesion = sesion;
        PuedeVer = sesion.Puede(Permisos.Respaldos.Ejecutar);

        RespaldarCommand = new RelayCommand(() => _ = Respaldar(), () => PuedeVer && !Trabajando);
        AbrirCarpetaCommand = new RelayCommand(AbrirCarpeta);
    }

    public bool PuedeVer { get; }

    public ICommand RespaldarCommand { get; }
    public ICommand AbrirCarpetaCommand { get; }

    public ObservableCollection<RegistroRespaldo> Historial { get; } = [];

    public string CarpetaLocal => _servicio.CarpetaLocal;

    private bool _trabajando;
    public bool Trabajando
    {
        get => _trabajando;
        private set
        {
            if (SetProperty(ref _trabajando, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _estado = string.Empty;
    public string Estado
    {
        get => _estado;
        private set => SetProperty(ref _estado, value);
    }

    public void CargarSiHaceFalta()
    {
        if (PuedeVer && Historial.Count == 0)
            Cargar();
    }

    private void Cargar()
    {
        try
        {
            Historial.Clear();
            foreach (var r in _servicio.Historial())
                Historial.Add(r);

            var ultimo = Historial.FirstOrDefault(r => r.Exitoso);
            Estado = ultimo is null
                ? "Todavía no hay ningún respaldo correcto."
                : $"Último respaldo correcto: {ultimo.FechaTexto} ({ultimo.TipoTexto}).";
        }
        catch (Exception ex)
        {
            Registro.Error(ex, "No se pudo leer el historial de respaldos");
            Estado = $"No se pudo leer el historial: {ex.Message}";
        }
    }

    private async Task Respaldar()
    {
        Trabajando = true;
        Estado = "Respaldando… puede tardar unos minutos.";
        try
        {
            var usuario = _sesion.UsuarioActual?.NombreCompleto ?? string.Empty;
            var resultado = await Task.Run(() => _servicio.Respaldar(OrigenRespaldo.Manual, usuario));
            Cargar();
            _dialogos.Informar(resultado.TodoBien ? "Respaldo hecho" : "Respaldo con problemas", resultado.Resumen);
        }
        catch (Exception ex)
        {
            Registro.Error(ex, "Falló el respaldo manual");
            Cargar();
            _dialogos.Informar("No se pudo respaldar", ex.Message);
        }
        finally
        {
            Trabajando = false;
        }
    }

    private void AbrirCarpeta()
    {
        try
        {
            System.IO.Directory.CreateDirectory(CarpetaLocal);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{CarpetaLocal}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Registro.Advertencia(ex, "No se pudo abrir la carpeta de respaldos");
        }
    }
}
