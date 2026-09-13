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

        CommandManager.InvalidateRequerySuggested();
    }

    public void CerrarSesion()
    {
        UsuarioActual = null;
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

    /// <summary>
    /// Base del rol + concedidos - revocados. Lo revocado gana sobre lo concedido: si un
    /// permiso aparece en las dos formas, el ajuste restrictivo es el que se respeta.
    /// </summary>
    private static IReadOnlySet<string> Calcular(Usuario usuario, IEnumerable<PermisoUsuario>? ajustes)
    {
        var efectivos = new HashSet<string>(MatrizPermisos.Base(usuario.Rol));

        if (ajustes is null)
            return efectivos;

        var propios = ajustes.Where(a => a.UsuarioId == usuario.Id).ToList();

        foreach (var concedido in propios.Where(a => a.Concedido))
            efectivos.Add(concedido.Permiso);

        foreach (var revocado in propios.Where(a => !a.Concedido))
            efectivos.Remove(revocado.Permiso);

        return efectivos;
    }
}
