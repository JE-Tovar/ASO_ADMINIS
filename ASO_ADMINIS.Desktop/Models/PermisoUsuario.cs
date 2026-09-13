namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Ajuste puntual sobre el conjunto de permisos que el rol ya trae: concede uno que el rol
/// no da, o revoca uno que si daba. Es el "permisos ajustables por usuario".
///
/// El calculo vive en <c>SesionActual.IniciarSesion</c>: base del rol + concedidos - revocados,
/// resuelto una sola vez al entrar.
/// </summary>
public class PermisoUsuario : IEntidad<int>, IDeOrganizacion
{
    public int Id { get; set; }
    public int OrganizacionId { get; set; }
    public int UsuarioId { get; set; }

    /// <summary>Snapshot del nombre de usuario, para que la grilla no muestre un Id suelto.</summary>
    public string UsuarioNombre { get; set; } = string.Empty;

    /// <summary>Permiso en formato "Modulo.Accion", tal como lo piden los comandos.</summary>
    public string Permiso { get; set; } = string.Empty;

    /// <summary>true concede, false revoca. Un revocado gana sobre lo que da el rol.</summary>
    public bool Concedido { get; set; }

    public string EfectoTexto => Concedido ? "Concedido" : "Revocado";

    public PermisoUsuario Clonar() => (PermisoUsuario)MemberwiseClone();
}
