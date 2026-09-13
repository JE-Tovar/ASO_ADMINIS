using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Los datos de la organización donde está instalado el sistema. No es un padrón: una
/// instalación atiende a una sola organización, que nace en el primer arranque. Aquí solo se
/// corrigen sus datos.
///
/// Al guardar se refresca <see cref="Ambito"/>: las pantallas siguientes deben ver el nombre o
/// el código nuevo sin esperar a que el usuario vuelva a entrar.
/// </summary>
public sealed class DatosOrganizacionViewModel : ViewModelBase
{
    private readonly IOrganizacionDataSource _organizaciones;
    private readonly IServicioDialogo _dialogos;
    private readonly TasaCambioService _tasaCambio;

    public DatosOrganizacionViewModel(IOrganizacionDataSource organizaciones,
                                      IServicioDialogo dialogos,
                                      ISesionActual sesion,
                                      TasaCambioService tasaCambio)
    {
        _organizaciones = organizaciones;
        _dialogos = dialogos;
        _tasaCambio = tasaCambio;

        PuedeEditar = sesion.Puede(Permisos.Organizacion.Editar);

        if (Ambito.Actual is { } organizacion)
        {
            _codigo = organizacion.Codigo;
            _nombre = organizacion.Nombre;
        }

        GuardarCommand = new RelayCommand(Guardar, () => PuedeEditar);
        ActualizarTasaCommand = new RelayCommand(() => _ = ActualizarTasa(), () => PuedeEditar && !Actualizando);

        ActualizarTextoTasa();
    }

    public bool PuedeEditar { get; }

    public ICommand GuardarCommand { get; }
    public ICommand ActualizarTasaCommand { get; }

    private bool _actualizando;
    public bool Actualizando
    {
        get => _actualizando;
        private set
        {
            if (SetProperty(ref _actualizando, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _tasaActualTexto = string.Empty;
    public string TasaActualTexto
    {
        get => _tasaActualTexto;
        private set => SetProperty(ref _tasaActualTexto, value);
    }

    private void ActualizarTextoTasa()
    {
        var actual = _tasaCambio.Actual();
        TasaActualTexto = actual is null
            ? "Sin tasa registrada todavía."
            : $"Bs. {actual.ValorTexto} por US$1 · BCV, {actual.FechaTexto}";
    }

    private async Task ActualizarTasa()
    {
        Actualizando = true;
        try
        {
            await _tasaCambio.ActualizarDesdeApi();
            ActualizarTextoTasa();
        }
        catch (Exception ex)
        {
            ActualizarTextoTasa();
            _dialogos.Informar("No se pudo actualizar la tasa",
                $"{ex.Message}\n\nSe sigue usando la última tasa guardada: {TasaActualTexto}");
        }
        finally
        {
            Actualizando = false;
        }
    }

    private string _codigo = string.Empty;
    public string Codigo
    {
        get => _codigo;
        set => SetProperty(ref _codigo, value);
    }

    private string _nombre = string.Empty;
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

    private void Guardar()
    {
        if (Ambito.Actual is not { } actual)
            return;

        if (string.IsNullOrWhiteSpace(Codigo) || string.IsNullOrWhiteSpace(Nombre))
        {
            _dialogos.Informar("Datos incompletos",
                "El código interno y el nombre de la organización son obligatorios.");
            return;
        }

        var actualizado = actual.Clonar();
        actualizado.Codigo = Codigo.Trim().ToUpperInvariant();
        actualizado.Nombre = Nombre.Trim();

        try
        {
            _organizaciones.Update(actualizado);
            Ambito.Actualizar(actualizado);

            Codigo = actualizado.Codigo;
            Nombre = actualizado.Nombre;

            _dialogos.Informar("Organización actualizada", "Los datos se guardaron correctamente.");
        }
        catch (Exception ex)
        {
            _dialogos.Informar("No se pudo guardar", ex.Message);
        }
    }
}
