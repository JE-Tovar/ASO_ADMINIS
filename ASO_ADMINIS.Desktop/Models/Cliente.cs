namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Cliente al que se le factura. Dato maestro (CRUD simple, arquetipo <see cref="Proveedor"/>).
///
/// La identificación es un RIF con dígito verificador o una cédula (ver Services/Rif.cs),
/// guardada ya normalizada ("J-12345678-9", "V-12345678"). La dirección fiscal es obligatoria
/// solo si el cliente es contribuyente (tiene RIF), porque la necesita para el crédito fiscal.
///
/// Las ventas a consumidor final no necesitan cliente: el documento lleva un snapshot
/// "Consumidor final" sin identificación.
/// </summary>
public class Cliente : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoAdminisDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>RIF o C.I., normalizado. Único por organización.</summary>
    public string Rif { get; set; } = string.Empty;

    /// <summary>Nombre y apellido, o razón social completa.</summary>
    public string Nombre { get; set; } = string.Empty;

    public string DireccionFiscal { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public string EstadoTexto => Activo ? "Activo" : "Inactivo";
    public string Etiqueta => $"{Rif} · {Nombre}";

    /// <summary>Copia superficial (solo hay tipos de valor y cadenas) para no mutar el original en la lista.</summary>
    public Cliente Clonar() => (Cliente)MemberwiseClone();
}
