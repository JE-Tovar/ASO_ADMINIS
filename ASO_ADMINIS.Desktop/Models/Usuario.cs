namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Usuario del sistema. Pertenece a una organizacion (nucleo) y esa pertenencia es la que
/// fija el ambito de la sesion al autenticar.
///
/// La contrasenna se guarda como hash PBKDF2 con salt por usuario; el texto plano no se
/// almacena ni se transporta (ver <c>Services/Passwords.cs</c>).
/// </summary>
public class Usuario : IEntidad<int>, IDeOrganizacion
{
    public int Id { get; set; }

    /// <summary>Nucleo al que pertenece. Un Desarrollador arranca en el suyo y puede cambiar.</summary>
    public int OrganizacionId { get; set; }

    /// <summary>Unico en TODO el sistema, no solo dentro de la organizacion: el login no
    /// sabe todavia a que nucleo pertenece quien escribe, asi que no puede desempatar.</summary>
    public string NombreUsuario { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;
    public Rol Rol { get; set; }

    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    // Sin arco de descarte: con `_ => "Desarrollador"` un rol nuevo se mostraba con el nombre
    // del rol que lo puede todo, que es la etiqueta mas enganosa posible. Ver MatrizPermisos.Base.
#pragma warning disable CS8524
    public string RolTexto => Rol switch
    {
        Rol.Operador => "Operador",
        Rol.Supervisor => "Supervisor",
        Rol.AdministradorOrganizacion => "Administrador de organización",
        Rol.Desarrollador => "Desarrollador"
    };
#pragma warning restore CS8524

    public string EstadoTexto => Activo ? "Activo" : "Inactivo";

    public Usuario Clonar() => (Usuario)MemberwiseClone();
}
