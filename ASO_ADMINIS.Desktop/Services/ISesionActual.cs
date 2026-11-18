using System.Collections.Generic;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Usuario con sesion activa. Es la unica puerta de autorizacion de la aplicacion:
/// todos los comandos preguntan aqui antes de habilitarse.
/// </summary>
/// <summary>
/// El usuario se autenticó bien pero no puede entrar (un cajero sin caja asignada). El mensaje
/// es para mostrarlo tal cual en el login.
/// </summary>
public sealed class SesionRechazadaException(string mensaje) : Exception(mensaje);

/// <summary>
/// Otro usuario modificó o eliminó el registro mientras se editaba (su rowversion ya no es la que
/// se leyó). No se pisa su cambio: se avisa y se recarga.
/// </summary>
public sealed class ConflictoDeEdicionException(Exception interna) : InvalidOperationException(
    "Otro usuario modificó o eliminó este registro mientras lo editabas, y no se guardó tu cambio " +
    "para no pisar el suyo. La lista se recargó: ábrelo de nuevo y repite el cambio.", interna);

public interface ISesionActual
{
    Usuario? UsuarioActual { get; }
    bool EstaAutenticado { get; }

    /// <summary>
    /// La caja de la sesión: la asignada al usuario (<see cref="Usuario.CajaId"/>), no la de la
    /// PC. Nula si no tiene, o si la suya está inactiva; sin caja no se factura.
    /// </summary>
    Caja? CajaActual { get; }

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
