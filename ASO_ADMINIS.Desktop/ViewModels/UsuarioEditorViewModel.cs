using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>Un rol con su nombre legible: el combo no debe mostrar "AdministradorNucleo".</summary>
public sealed record OpcionRol(Rol Valor, string Texto);

/// <summary>
/// Alta y edición de un usuario del núcleo.
///
/// La contraseña se escribe aquí pero no se guarda: al aceptar se convierte en hash + salt
/// (<see cref="Passwords"/>) y el texto plano se descarta. Al editar, dejarla vacía deja la
/// que ya tenía — así se puede cambiar el nombre o el rol sin conocer su clave.
/// </summary>
public sealed class UsuarioEditorViewModel : CrudEditorViewModelBase<Usuario>
{
    private const int LargoMinimoPassword = 8;

    private readonly Usuario _original;

    public UsuarioEditorViewModel(Usuario original, ISesionActual? sesion = null)
    {
        _original = original;
        var actual = sesion ?? SesionActual.Instancia;

        _nombreUsuario = original.NombreUsuario;
        _nombreCompleto = original.NombreCompleto;
        var rolInicial = original.Id == 0 ? Rol.Operador : original.Rol;
        _activo = original.Id == 0 || original.Activo;

        // Un administrador de organización no puede fabricar un Desarrollador: ese rol lo
        // puede todo, así que solo lo reparte quien ya lo tiene.
        Rol[] asignables = actual.Puede(Permisos.Usuarios.CrearDesarrollador)
            ? [Rol.Operador, Rol.Supervisor, Rol.AdministradorOrganizacion, Rol.Desarrollador]
            : [Rol.Operador, Rol.Supervisor, Rol.AdministradorOrganizacion];

        RolesDisponibles = [.. asignables.Select(r => new OpcionRol(r, Texto(r)))];
        _rolSeleccionado = RolesDisponibles.FirstOrDefault(o => o.Valor == rolInicial) ?? RolesDisponibles[0];

    }

    public override string Titulo => EsNuevo ? "Nuevo usuario" : $"Usuario: {_original.NombreUsuario}";

    public bool EsNuevo => _original.Id == 0;

    public IReadOnlyList<OpcionRol> RolesDisponibles { get; }

    public string EtiquetaPassword => EsNuevo
        ? "Contraseña"
        : "Contraseña nueva (dejar vacío para no cambiarla)";

    private string _nombreUsuario;
    public string NombreUsuario
    {
        get => _nombreUsuario;
        set => SetProperty(ref _nombreUsuario, value);
    }

    private string _nombreCompleto;
    public string NombreCompleto
    {
        get => _nombreCompleto;
        set => SetProperty(ref _nombreCompleto, value);
    }

    private OpcionRol _rolSeleccionado;
    public OpcionRol RolSeleccionado
    {
        get => _rolSeleccionado;
        set => SetProperty(ref _rolSeleccionado, value);
    }

    // Sin arco de descarte, igual que Usuario.RolTexto: un rol nuevo sin mapear tiene que
    // romper la compilacion, no aparecer en el combo llamandose "Desarrollador".
#pragma warning disable CS8524
    private static string Texto(Rol rol) => rol switch
    {
        Rol.Operador => "Operador",
        Rol.Supervisor => "Supervisor",
        Rol.AdministradorOrganizacion => "Administrador de organización",
        Rol.Desarrollador => "Desarrollador"
    };
#pragma warning restore CS8524

    private bool _activo;
    public bool Activo
    {
        get => _activo;
        set => SetProperty(ref _activo, value);
    }

    /// <summary>Texto plano, transitorio: solo vive mientras el editor está abierto.</summary>
    private string _passwordNueva = string.Empty;
    public string PasswordNueva
    {
        get => _passwordNueva;
        set => SetProperty(ref _passwordNueva, value);
    }

    protected override bool Validar(out string? error)
    {
        if (string.IsNullOrWhiteSpace(NombreUsuario))
        {
            error = "Indique el nombre de usuario.";
            return false;
        }

        if (NombreUsuario.Trim().Contains(' '))
        {
            error = "El nombre de usuario no puede llevar espacios.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(NombreCompleto))
        {
            error = "Indique el nombre completo.";
            return false;
        }

        var cambiaPassword = EsNuevo || !string.IsNullOrEmpty(PasswordNueva);
        if (cambiaPassword && PasswordNueva.Length < LargoMinimoPassword)
        {
            error = $"La contraseña debe tener al menos {LargoMinimoPassword} caracteres.";
            return false;
        }

        error = null;
        return true;
    }

    public override Usuario ObtenerResultado()
    {
        var resultado = _original.Clonar();
        resultado.NombreUsuario = NombreUsuario.Trim();
        resultado.NombreCompleto = NombreCompleto.Trim();
        resultado.Rol = RolSeleccionado.Valor;
        resultado.Activo = Activo;

        if (!string.IsNullOrEmpty(PasswordNueva))
        {
            var (hash, salt) = Passwords.Crear(PasswordNueva);
            resultado.PasswordHash = hash;
            resultado.PasswordSalt = salt;
        }

        return resultado;
    }
}
