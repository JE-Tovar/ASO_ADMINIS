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
            _razonSocial = organizacion.RazonSocial;
            _rif = organizacion.Rif;
            _direccionFiscal = organizacion.DireccionFiscal;
            _telefono = organizacion.Telefono;
            _contribuyenteEspecial = organizacion.ContribuyenteEspecial;
            _ivaGeneral = organizacion.IvaGeneral.ToString("0.##");
            _ivaReducida = organizacion.IvaReducida.ToString("0.##");
            _ivaAdicional = organizacion.IvaAdicional.ToString("0.##");
            _igtf = organizacion.IgtfPorcentaje.ToString("0.##");
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

    // ---- Datos fiscales del emisor ----

    private string _razonSocial = string.Empty;
    public string RazonSocial
    {
        get => _razonSocial;
        set => SetProperty(ref _razonSocial, value);
    }

    private string _rif = string.Empty;
    public string Rif
    {
        get => _rif;
        set
        {
            if (SetProperty(ref _rif, value))
                OnPropertyChanged(nameof(RifAyuda));
        }
    }

    public string RifAyuda => string.IsNullOrWhiteSpace(Rif)
        ? "J-12345678-9. Sin RIF no se pueden emitir documentos fiscales."
        : Services.Rif.Error(Rif, exigirRif: true) ?? $"✓ {Services.Rif.Normalizar(Rif)}";

    private string _direccionFiscal = string.Empty;
    public string DireccionFiscal
    {
        get => _direccionFiscal;
        set => SetProperty(ref _direccionFiscal, value);
    }

    private string _telefono = string.Empty;
    public string Telefono
    {
        get => _telefono;
        set => SetProperty(ref _telefono, value);
    }

    private bool _contribuyenteEspecial;
    public bool ContribuyenteEspecial
    {
        get => _contribuyenteEspecial;
        set => SetProperty(ref _contribuyenteEspecial, value);
    }

    private string _ivaGeneral = "16";
    public string IvaGeneral
    {
        get => _ivaGeneral;
        set => SetProperty(ref _ivaGeneral, value);
    }

    private string _ivaReducida = "8";
    public string IvaReducida
    {
        get => _ivaReducida;
        set => SetProperty(ref _ivaReducida, value);
    }

    private string _ivaAdicional = "31";
    public string IvaAdicional
    {
        get => _ivaAdicional;
        set => SetProperty(ref _ivaAdicional, value);
    }

    private string _igtf = "3";
    public string Igtf
    {
        get => _igtf;
        set => SetProperty(ref _igtf, value);
    }

    private static bool LeerPorcentaje(string texto, out decimal valor) =>
        decimal.TryParse(texto, out valor) && valor >= 0 && valor < 100;

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

        if (!string.IsNullOrWhiteSpace(Rif) && Services.Rif.Error(Rif, exigirRif: true) is { } errorRif)
        {
            _dialogos.Informar("RIF no válido", errorRif);
            return;
        }

        if (!LeerPorcentaje(IvaGeneral, out var ivaGeneral)
            || !LeerPorcentaje(IvaReducida, out var ivaReducida)
            || !LeerPorcentaje(IvaAdicional, out var ivaAdicional)
            || !LeerPorcentaje(Igtf, out var igtf))
        {
            _dialogos.Informar("Porcentajes no válidos",
                "Los porcentajes de IVA e IGTF deben ser números entre 0 y 99,99.");
            return;
        }

        var actualizado = actual.Clonar();
        actualizado.Codigo = Codigo.Trim().ToUpperInvariant();
        actualizado.Nombre = Nombre.Trim();
        actualizado.RazonSocial = RazonSocial.Trim();
        actualizado.Rif = Services.Rif.Normalizar(Rif) ?? string.Empty;
        actualizado.DireccionFiscal = DireccionFiscal.Trim();
        actualizado.Telefono = Telefono.Trim();
        actualizado.ContribuyenteEspecial = ContribuyenteEspecial;
        actualizado.IvaGeneral = ivaGeneral;
        actualizado.IvaReducida = ivaReducida;
        actualizado.IvaAdicional = ivaAdicional;
        actualizado.IgtfPorcentaje = igtf;

        try
        {
            _organizaciones.Update(actualizado);
            Ambito.Actualizar(actualizado);

            Codigo = actualizado.Codigo;
            Nombre = actualizado.Nombre;
            Rif = actualizado.Rif;

            _dialogos.Informar("Organización actualizada", "Los datos se guardaron correctamente.");
        }
        catch (Exception ex)
        {
            _dialogos.Informar("No se pudo guardar", ex.Message);
        }
    }
}
