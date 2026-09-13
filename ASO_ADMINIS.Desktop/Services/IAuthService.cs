using System.Collections.Generic;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Lo que hace falta para abrir sesion: el usuario y sus ajustes de permisos, leidos juntos
/// porque el conjunto efectivo se calcula de una sola vez al entrar.
/// </summary>
public sealed record ResultadoAutenticacion(Usuario Usuario, IReadOnlyList<PermisoUsuario> Ajustes);

/// <summary>Valida credenciales de acceso contra el padron de usuarios.</summary>
public interface IAuthService
{
    /// <returns>El usuario y sus ajustes si las credenciales son validas; <c>null</c> si no.</returns>
    ResultadoAutenticacion? ValidarCredenciales(string nombreUsuario, string password);
}
