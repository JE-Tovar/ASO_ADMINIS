namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Proveedor del centro: repuestos, combustible, taller externo, servicios.
/// Dato maestro; cruza zafras, no lleva <c>ZafraId</c>.
/// </summary>
public class Proveedor : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Nucleo (organizacion) duenno de la fila; lo estampa AsoAdminisDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Registro de información fiscal, como lo exige la factura de compra.</summary>
    public string Rif { get; set; } = string.Empty;

    public string Telefono { get; set; } = string.Empty;
    public string Notas { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public string EstadoTexto => Activo ? "Activo" : "Inactivo";

    public string Etiqueta => string.IsNullOrWhiteSpace(Rif) ? Nombre : $"{Nombre} · {Rif}";

    /// <summary>Copia superficial (solo hay tipos de valor y cadenas) para no mutar el original en la lista.</summary>
    public Proveedor Clonar() => (Proveedor)MemberwiseClone();
}
