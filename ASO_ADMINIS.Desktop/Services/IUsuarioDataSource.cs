using System.Collections.Generic;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>Usuarios del sistema. El listado corriente ya viene acotado a la organizacion activa.</summary>
public interface IUsuarioDataSource : ICrudDataSource<Usuario, int>
{
    /// <summary>
    /// Busca por nombre de usuario SIN filtrar por organizacion: al autenticar todavia no se
    /// sabe a que nucleo pertenece quien escribe. Es el unico uso legitimo aparte del login.
    /// </summary>
    Usuario? BuscarPorNombreSinAmbito(string nombreUsuario);

    /// <summary>Ajustes de permisos de un usuario, tambien sin filtro de ambito (se lee al entrar).</summary>
    IReadOnlyList<PermisoUsuario> AjustesDe(int usuarioId);

    /// <summary>¿Hay al menos un usuario en TODO el sistema? Decide si hay que sembrar.</summary>
    bool ExisteAlguno();

    /// <summary>
    /// Suma un intento fallido y, al llegar a <paramref name="limite"/>, bloquea la cuenta hasta
    /// <paramref name="bloquearHasta"/> (y vuelve el contador a cero). Sin ámbito: es del login.
    /// </summary>
    /// <returns>Cómo quedó el usuario.</returns>
    Usuario RegistrarIntentoFallido(int usuarioId, int limite, DateTime bloquearHasta);

    /// <summary>Contador a cero y sin bloqueo: entró bien. Sin ámbito: es del login.</summary>
    void ReiniciarIntentos(int usuarioId);

    /// <summary>
    /// El usuario tal como está ahora en la base, con sus ajustes de permisos, para refrescar la
    /// sesión abierta. Sin ámbito, como el login. Null si ya no existe.
    /// </summary>
    ResultadoAutenticacion? LeerParaSesion(int usuarioId);
}
