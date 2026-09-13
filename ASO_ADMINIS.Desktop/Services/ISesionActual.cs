using System.Collections.Generic;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Usuario con sesion activa. Es la unica puerta de autorizacion de la aplicacion:
/// todos los comandos preguntan aqui antes de habilitarse.
/// </summary>
public interface ISesionActual
{
    Usuario? UsuarioActual { get; }
    bool EstaAutenticado { get; }

    /// <summary>
    /// Abre sesion, calcula el conjunto efectivo de permisos (base del rol + concedidos
    /// - revocados) y fija el ambito de organizacion a partir del usuario.
    /// </summary>
    void IniciarSesion(Usuario usuario, IEnumerable<PermisoUsuario>? ajustes = null);

    void CerrarSesion();

    /// <summary>¿El usuario actual puede realizar <paramref name="permiso"/> ("Modulo.Accion")?</summary>
    bool Puede(string permiso);

    /// <summary>
    /// No puede, pero el permiso es de los sensibles y el usuario tiene derecho a pedirlo:
    /// en vez de un boton muerto, el comando abre una peticion al administrador del nucleo.
    /// </summary>
    bool PuedeSolicitar(string permiso);
}
