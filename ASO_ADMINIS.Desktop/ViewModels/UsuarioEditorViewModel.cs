using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>Un rol con su nombre legible: el combo no debe mostrar "AdministradorNucleo".</summary>
public sealed record OpcionRol(Rol Valor, string Texto);

/// <summary>Una caja del combo del editor, o "Sin caja" (<see cref="Id"/> nulo).</summary>
public sealed record OpcionCaja(int? Id, string Texto);

/// <summary>
/// Alta y edición de un usuario del núcleo.
///
/// La contraseña se escribe aquí pero no se guarda: al aceptar se convierte en hash + salt
/// (<see cref="Passwords"/>) y el texto plano se descarta. Al editar, dejarla vacía deja la
/// que ya tenía — así se puede cambiar el nombre o el rol sin conocer su clave.
/// </summary>
public sealed class UsuarioEditorViewModel : CrudEditorViewModelBase<Usuario>
{

    private readonly Usuario _original;

    public UsuarioEditorViewModel(Usuario original, ISesionActual? sesion = null, IEnumerable<Caja>? cajas = null)
    {
        _original = original;
        var actual = sesion ?? SesionActual.Instancia;
        _esUnoMismo = original.Id != 0 && original.Id == actual.UsuarioActual?.Id;

        _nombreUsuario = original.NombreUsuario;
        _nombreCompleto = original.NombreCompleto;
        var rolInicial = original.Id == 0 ? Rol.Cajero : original.Rol;
        _activo = original.Id == 0 || original.Activo;

        // Un administrador de organización no puede fabricar un Desarrollador: ese rol lo
        // puede todo, así que solo lo reparte quien ya lo tiene.
        Rol[] asignables = actual.Puede(Permisos.Usuarios.CrearDesarrollador)
            ? [Rol.Cajero, Rol.Supervisor, Rol.AdministradorOrganizacion, Rol.Desarrollador]
            : [Rol.Cajero, Rol.Supervisor, Rol.AdministradorOrganizacion];

        RolesDisponibles = [.. asignables.Select(r => new OpcionRol(r, Texto(r)))];
        _rolSeleccionado = RolesDisponibles.FirstOrDefault(o => o.Valor == rolInicial) ?? RolesDisponibles[0];

        // Se ofrecen las activas, más la que ya tenía aunque se haya desactivado: si no, abrir el
        // editor le quitaría la caja en silencio.
        var lista = (cajas ?? [])
            .Where(c => c.Activa || c.Id == original.CajaId)
            .OrderBy(c => c.Codigo)
            .Select(c => new OpcionCaja(c.Id, c.Activa ? c.Etiqueta : $"{c.Etiqueta} (inactiva)"));
        CajasDisponibles = [new OpcionCaja(null, "Sin caja"), .. lista];
        _cajaSeleccionada = CajasDisponibles.FirstOrDefault(o => o.Id == original.CajaId) ?? CajasDisponibles[0];
    }

    public IReadOnlyList<OpcionCaja> CajasDisponibles { get; }

    private readonly bool _esUnoMismo;

    /// <summary>"Bloqueada hasta las 10:42 por intentos fallidos", o vacío.</summary>
    public string BloqueoTexto => _original.EstaBloqueado(DateTime.Now)
        ? $"Cuenta bloqueada hasta las {_original.BloqueadoHasta:HH:mm} por intentos fallidos."
        : string.Empty;

    public bool EstaBloqueado => BloqueoTexto.Length > 0;

    private bool _desbloquear;
    public bool Desbloquear
    {
        get => _desbloquear;
        set => SetProperty(ref _desbloquear, value);
    }

    /// <summary>
    /// Si se pone la contraseña de OTRA persona, esa persona tiene que cambiarla al entrar: la
    /// conoce también el administrador. La propia se cambia mejor en Configuración · Mi cuenta.
    /// </summary>
    public string AyudaPassword => _esUnoMismo
        ? PoliticaSeguridad.DescripcionPassword
        : $"{PoliticaSeguridad.DescripcionPassword} Al entrar, el usuario tendrá que cambiarla por una suya.";

    private OpcionCaja _cajaSeleccionada;
    public OpcionCaja CajaSeleccionada
    {
        get => _cajaSeleccionada;
        set => SetProperty(ref _cajaSeleccionada, value);
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
        Rol.OperadorRetirado => "Operador (retirado)",
        Rol.Supervisor => "Supervisor",
        Rol.AdministradorOrganizacion => "Administrador de organización",
        Rol.Desarrollador => "Desarrollador",
        Rol.Cajero => "Cajero"
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

        if (RolSeleccionado.Valor == Rol.Cajero && CajaSeleccionada.Id is null)
        {
            error = "Un usuario Cajero necesita una caja asignada. Créala antes en Administración · Cajas si no existe.";
            return false;
        }

        var cambiaPassword = EsNuevo || !string.IsNullOrEmpty(PasswordNueva);
        if (cambiaPassword && PoliticaSeguridad.ErrorPassword(PasswordNueva, NombreUsuario) is { } errorPassword)
        {
            error = errorPassword;
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
        resultado.CajaId = CajaSeleccionada.Id;

        if (!string.IsNullOrEmpty(PasswordNueva))
        {
            var (hash, salt) = Passwords.Crear(PasswordNueva);
            resultado.PasswordHash = hash;
            resultado.PasswordSalt = salt;
            resultado.DebeCambiarPassword = !_esUnoMismo;

            // Poner una contraseña nueva también levanta el bloqueo: es lo que se hace cuando
            // alguien la olvidó a fuerza de probar.
            resultado.IntentosFallidos = 0;
            resultado.BloqueadoHasta = null;
        }

        if (Desbloquear)
        {
            resultado.IntentosFallidos = 0;
            resultado.BloqueadoHasta = null;
        }

        return resultado;
    }
}
