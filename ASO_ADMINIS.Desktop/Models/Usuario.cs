namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Usuario del sistema. Pertenece a una organizacion (nucleo) y esa pertenencia es la que
/// fija el ambito de la sesion al autenticar.
///
/// La contrasenna se guarda como hash PBKDF2 con salt por usuario; el texto plano no se
/// almacena ni se transporta (ver <c>Services/Passwords.cs</c>).
/// </summary>
public class Usuario : IEntidad<int>, IDeOrganizacion, IConVersion
{
    /// <summary>Concurrencia optimista (rowversion): la pone SQL Server en cada escritura.</summary>
    public byte[]? Version { get; set; }

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

    /// <summary>
    /// Caja asignada (<see cref="Caja"/>). Obligatoria para un <see cref="Rol.Cajero"/>; opcional
    /// para el resto (un supervisor que también despacha). Es la caja de la sesión: sin ella no
    /// se factura.
    /// </summary>
    public int? CajaId { get; set; }

    // ---- Seguridad de la cuenta (ver Services/PoliticaSeguridad.cs) ----

    /// <summary>Intentos fallidos seguidos. Vuelve a cero al entrar bien o al desbloquear.</summary>
    public int IntentosFallidos { get; set; }

    /// <summary>Hasta cuándo no puede entrar, tras demasiados intentos fallidos. Nulo: no está bloqueada.</summary>
    public DateTime? BloqueadoHasta { get; set; }

    /// <summary>
    /// Tiene que cambiar la contraseña al entrar: la puso otra persona (el administrador al
    /// crearla o al reiniciarla), así que no es solo suya.
    /// </summary>
    public bool DebeCambiarPassword { get; set; }

    public bool EstaBloqueado(DateTime ahora) => BloqueadoHasta is { } hasta && hasta > ahora;

    /// <summary>Código de la caja asignada, para la grilla. No se persiste: lo llena la fuente de datos.</summary>
    public string CajaTexto { get; set; } = string.Empty;

    // Sin arco de descarte: con `_ => "Desarrollador"` un rol nuevo se mostraba con el nombre
    // del rol que lo puede todo, que es la etiqueta mas enganosa posible. Ver MatrizPermisos.Base.
#pragma warning disable CS8524
    public string RolTexto => Rol switch
    {
        Rol.OperadorRetirado => "Operador (retirado)",
        Rol.Supervisor => "Supervisor",
        Rol.AdministradorOrganizacion => "Administrador de organización",
        Rol.Desarrollador => "Desarrollador",
        Rol.Cajero => "Cajero"
    };
#pragma warning restore CS8524

    public string EstadoTexto => Activo ? "Activo" : "Inactivo";

    public Usuario Clonar() => (Usuario)MemberwiseClone();
}
