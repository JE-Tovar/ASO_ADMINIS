using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>Ajustes de permisos por usuario, administrados dentro de la organizacion activa.</summary>
public interface IPermisoUsuarioDataSource : ICrudDataSource<PermisoUsuario, int>
{
}
