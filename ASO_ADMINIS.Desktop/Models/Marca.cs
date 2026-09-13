namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Marca con la que trabaja la organización (p. ej. un fabricante de calzado). Dato maestro,
/// arquetipo CRUD simple igual que <see cref="Proveedor"/>.
/// </summary>
public class Marca : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoAdminisDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Notas { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public string EstadoTexto => Activo ? "Activo" : "Inactivo";

    public string Etiqueta => Nombre;

    /// <summary>Copia superficial (solo hay tipos de valor y cadenas) para no mutar el original en la lista.</summary>
    public Marca Clonar() => (Marca)MemberwiseClone();
}
