using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Sesion en memoria, una por proceso.
///
/// El conjunto de permisos se resuelve UNA vez al iniciar sesion y queda cacheado, no se
/// recalcula en cada consulta: <c>CommandManager.RequerySuggested</c> dispara los CanExecute
/// de toda la ventana ante cualquier entrada del usuario, asi que <see cref="Puede"/> se llama
/// cientos de veces por minuto y tiene que ser una busqueda en tabla hash.
/// </summary>
public class SesionActual : ISesionActual
{
    private static readonly IReadOnlySet<string> _sinPermisos = new HashSet<string>();

    public static SesionActual Instancia { get; } = new();

    private IReadOnlySet<string> _permisos = _sinPermisos;

    public Usuario? UsuarioActual { get; private set; }
    public bool EstaAutenticado => UsuarioActual is not null;
    public Caja? CajaActual { get; private set; }

    public void IniciarSesion(Usuario usuario, IEnumerable<PermisoUsuario>? ajustes = null)
    {
        UsuarioActual = usuario;
        _permisos = Calcular(usuario, ajustes);

        // La pertenencia del usuario es la que fija el ambito: nadie elige su organizacion al
        // entrar y nadie la cambia despues (una instalacion atiende a una sola organizacion). El
        // padron de Organizaciones no lleva filtro de consulta, asi que se puede leer con el
        // ambito todavia sin fijar.
        var organizacion = DataSourceFactory.CrearOrganizaciones().GetById(usuario.OrganizacionId)
            ?? throw new InvalidOperationException(
                $"El usuario {usuario.NombreUsuario} apunta a la organizacion {usuario.OrganizacionId}, que no existe.");

        Ambito.Fijar(organizacion);

        // La caja se lee con el ámbito ya fijado: el padrón de cajas sí lleva filtro por
        // organización. Una caja inactiva cuenta como ninguna.
        CajaActual = usuario.CajaId is { } cajaId
            && DataSourceFactory.CrearCajas().GetById(cajaId) is { Activa: true } caja
                ? caja
                : null;

        // Un cajero sin caja no tiene nada que hacer dentro: no puede facturar ni ver nada más.
        // Se le dice en el login, en vez de dejarlo entrar a una pantalla que rechaza todo.
        if (usuario.Rol == Rol.Cajero && CajaActual is null)
        {
            CerrarSesion();
            throw new SesionRechazadaException(
                "Tu usuario es de caja pero no tiene una caja activa asignada. " +
                "Pídele al administrador que te la asigne en Administración · Usuarios.");
        }

        CommandManager.InvalidateRequerySuggested();
    }

    public void CerrarSesion()
    {
        UsuarioActual = null;
        CajaActual = null;
        _permisos = _sinPermisos;
        Ambito.Fijar(null);
        CommandManager.InvalidateRequerySuggested();
    }

    public bool Puede(string permiso) => _permisos.Contains(permiso);

    public bool PuedeSolicitar(string permiso) =>
        EstaAutenticado
        && !Puede(permiso)
        && Puede(Permisos.Peticiones.Solicitar)
        && MatrizPermisos.EsSolicitable(permiso);

    // ---------------------------------------------------------------- Refresco en caliente

    /// <summary>Lo que hay en la base para el usuario de la sesión, leído fuera del hilo de interfaz.</summary>
    public sealed record Lectura(int UsuarioId, ResultadoAutenticacion? Usuario, Caja? Caja, Organizacion? Organizacion);

    public enum Cambio
    {
        SinCambios,

        /// <summary>Cambió el rol, los permisos o la caja: ya se aplicó, hay que redibujar el menú.</summary>
        Actualizada,

        /// <summary>Lo desactivaron, lo borraron o un cajero se quedó sin caja: hay que cerrar la sesión.</summary>
        Revocada
    }

    /// <summary>
    /// Lee el estado actual del usuario de la sesión. No toca la sesión: se llama desde un hilo de
    /// fondo y lo aplica <see cref="Aplicar"/> en el de interfaz.
    /// </summary>
    public Lectura? Leer()
    {
        if (UsuarioActual is not { } usuario)
            return null;

        var leido = DataSourceFactory.CrearUsuarios().LeerParaSesion(usuario.Id);
        var caja = leido?.Usuario.CajaId is { } cajaId ? DataSourceFactory.CrearCajas().GetById(cajaId) : null;
        var organizacion = DataSourceFactory.CrearOrganizaciones().GetById(usuario.OrganizacionId);
        return new Lectura(usuario.Id, leido, caja, organizacion);
    }

    /// <summary>
    /// Aplica lo leído. Antes un cambio de rol o de permisos solo valía al volver a entrar, y
    /// desactivar a alguien no lo sacaba de una sesión abierta.
    /// </summary>
    public (Cambio Cambio, string? Motivo) Aplicar(Lectura? lectura)
    {
        if (lectura is null || UsuarioActual is not { } actual || actual.Id != lectura.UsuarioId)
            return (Cambio.SinCambios, null);

        if (lectura.Usuario is not { } leido || !leido.Usuario.Activo)
            return (Cambio.Revocada, "Un administrador desactivó tu usuario.");

        // Los datos de la organización también se refrescan (minutos de inactividad, datos
        // fiscales que cambió el administrador en otra PC). No obligan a redibujar el menú.
        if (lectura.Organizacion is { } organizacion && organizacion.Id == Ambito.OrganizacionId)
            Ambito.Actualizar(organizacion);

        var nuevo = leido.Usuario;
        var caja = lectura.Caja is { Activa: true } c ? c : null;
        if (nuevo.Rol == Rol.Cajero && caja is null)
            return (Cambio.Revocada, "Tu usuario se quedó sin una caja activa asignada.");

        var permisos = Calcular(nuevo, leido.Ajustes);
        var cambio = nuevo.Rol != actual.Rol
                     || nuevo.CajaId != actual.CajaId
                     || caja?.Id != CajaActual?.Id
                     || !permisos.SetEquals(_permisos);

        // La copia fresca reemplaza a la de la sesión aunque no cambie nada visible: trae la
        // rowversion vigente, y sin ella el próximo cambio de contraseña chocaría.
        UsuarioActual = nuevo;
        if (!cambio)
            return (Cambio.SinCambios, null);

        _permisos = permisos;
        CajaActual = caja;
        CommandManager.InvalidateRequerySuggested();
        Registro.Info("Permisos de la sesión actualizados en caliente (rol {Rol})", nuevo.RolTexto);
        return (Cambio.Actualizada, null);
    }

    /// <summary>
    /// Base del rol + concedidos - revocados. Lo revocado gana sobre lo concedido: si un
    /// permiso aparece en las dos formas, el ajuste restrictivo es el que se respeta.
    /// </summary>
    internal static IReadOnlySet<string> Calcular(Usuario usuario, IEnumerable<PermisoUsuario>? ajustes)
    {
        var efectivos = new HashSet<string>(MatrizPermisos.Base(usuario.Rol));

        var propios = (ajustes ?? []).Where(a => a.UsuarioId == usuario.Id).ToList();

        foreach (var concedido in propios.Where(a => a.Concedido))
            efectivos.Add(concedido.Permiso);

        foreach (var revocado in propios.Where(a => !a.Concedido))
            efectivos.Remove(revocado.Permiso);

        // Lo de supervisión no se gana con un ajuste: por debajo de Supervisor se quita siempre,
        // haya lo que haya en la base (ver MatrizPermisos.SoloSupervision).
        if (!MatrizPermisos.EsDeSupervision(usuario.Rol))
            efectivos.ExceptWith(MatrizPermisos.SoloSupervision);

        return efectivos;
    }
}
