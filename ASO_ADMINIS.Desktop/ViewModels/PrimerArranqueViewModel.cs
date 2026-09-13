using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Primera puesta en marcha: crea la organización inicial y el usuario Desarrollador que la
/// administra.
///
/// La contraseña se pide aquí en vez de sembrarse en una migración a propósito. Una clave por
/// defecto en el repositorio es una clave pública, y este es justo el usuario que lo puede todo.
/// </summary>
public sealed class PrimerArranqueViewModel : ViewModelBase
{
    private const int LargoMinimoPassword = 8;

    private readonly IOrganizacionDataSource _organizaciones;
    private readonly IUsuarioDataSource _usuarios;

    public PrimerArranqueViewModel()
        : this(DataSourceFactory.CrearOrganizaciones(), DataSourceFactory.CrearUsuarios())
    {
    }

    public PrimerArranqueViewModel(IOrganizacionDataSource organizaciones, IUsuarioDataSource usuarios)
    {
        _organizaciones = organizaciones;
        _usuarios = usuarios;

        // Si ya hay una organización sembrada (por ejemplo por una migración inicial), se ADOPTA
        // en vez de crear otra: si aquí naciera una organización nueva, el desarrollador entraría
        // a una vacía y no vería nada de lo que hay en la base.
        _existente = _organizaciones.GetAll().FirstOrDefault();

        if (_existente is { } organizacion)
        {
            _codigoOrganizacion = organizacion.Codigo;
            _nombreOrganizacion = organizacion.Nombre;
        }
    }

    private readonly Organizacion? _existente;

    public string Explicacion => _existente is null
        ? "La base de datos está vacía. Crea la organización y el usuario desarrollador que la administrará."
        : "La base de datos no tiene usuarios todavía. Ponle nombre a la organización que ya contiene los datos y crea el usuario desarrollador que la administrará.";

    private string _codigoOrganizacion = string.Empty;
    public string CodigoOrganizacion
    {
        get => _codigoOrganizacion;
        set => SetProperty(ref _codigoOrganizacion, value);
    }

    private string _nombreOrganizacion = string.Empty;
    public string NombreOrganizacion
    {
        get => _nombreOrganizacion;
        set => SetProperty(ref _nombreOrganizacion, value);
    }

    private string _nombreUsuario = string.Empty;
    public string NombreUsuario
    {
        get => _nombreUsuario;
        set => SetProperty(ref _nombreUsuario, value);
    }

    private string _nombreCompleto = string.Empty;
    public string NombreCompleto
    {
        get => _nombreCompleto;
        set => SetProperty(ref _nombreCompleto, value);
    }

    private string _mensajeError = string.Empty;
    public string MensajeError
    {
        get => _mensajeError;
        set => SetProperty(ref _mensajeError, value);
    }

    /// <summary>Crea la organización y el usuario. <c>true</c> si quedó todo listo para iniciar sesión.</summary>
    public bool Crear(string password)
    {
        if (!Validar(password))
            return false;

        try
        {
            Organizacion organizacion;

            if (_existente is { } actual)
            {
                organizacion = actual.Clonar();
                organizacion.Codigo = CodigoOrganizacion.Trim().ToUpperInvariant();
                organizacion.Nombre = NombreOrganizacion.Trim();
                organizacion.Activa = true;
                _organizaciones.Update(organizacion);
            }
            else
            {
                organizacion = _organizaciones.Add(new Organizacion
                {
                    Codigo = CodigoOrganizacion.Trim().ToUpperInvariant(),
                    Nombre = NombreOrganizacion.Trim(),
                    Activa = true
                });
            }

            var (hash, salt) = Passwords.Crear(password);

            // OrganizacionId explícito: el estampado automático de AsoAdminisDbContext.SaveChanges lee
            // el ámbito de la sesión, y todavía no hay ninguna.
            _usuarios.Add(new Usuario
            {
                OrganizacionId = organizacion.Id,
                NombreUsuario = NombreUsuario.Trim(),
                NombreCompleto = NombreCompleto.Trim(),
                Rol = Rol.Desarrollador,
                PasswordHash = hash,
                PasswordSalt = salt,
                Activo = true
            });
        }
        catch (Exception ex)
        {
            MensajeError = $"No se pudo crear la configuración inicial. {ex.Message}";
            return false;
        }

        MensajeError = string.Empty;
        return true;
    }

    private bool Validar(string password)
    {
        if (string.IsNullOrWhiteSpace(CodigoOrganizacion))
        {
            MensajeError = "Indique el código de la organización.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(NombreOrganizacion))
        {
            MensajeError = "Indique el nombre de la organización.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(NombreUsuario) || NombreUsuario.Trim().Contains(' '))
        {
            MensajeError = "Indique un nombre de usuario sin espacios.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(NombreCompleto))
        {
            MensajeError = "Indique el nombre completo.";
            return false;
        }

        if (password.Length < LargoMinimoPassword)
        {
            MensajeError = $"La contraseña debe tener al menos {LargoMinimoPassword} caracteres.";
            return false;
        }

        return true;
    }
}
