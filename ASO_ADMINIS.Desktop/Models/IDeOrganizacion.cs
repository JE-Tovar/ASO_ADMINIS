namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Marca una entidad como perteneciente a una <see cref="Organizacion"/>, es decir,
/// sujeta al aislamiento por nucleo.
///
/// Quien implementa esta interfaz gana dos cosas automaticas y no debe reimplementarlas:
/// el filtro global de consulta de <c>AsoAdminisDbContext.OnModelCreating</c> (nunca devuelve filas
/// de otra organizacion) y el estampado de <c>AsoAdminisDbContext.SaveChanges</c> (toda fila nueva
/// recibe la organizacion del ambito activo).
/// </summary>
public interface IDeOrganizacion
{
    int OrganizacionId { get; set; }
}
