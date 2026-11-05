namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// La empresa donde esta instalado el sistema. Una instalacion atiende a una sola organizacion,
/// y dentro de ella todas las referencias apuntan a esa misma organizacion.
///
/// Es tambien el ambito de aislamiento: cada fila de las entidades operativas le pertenece y
/// nadie ve las de otra (ver <see cref="IDeOrganizacion"/>).
/// </summary>
public class Organizacion : IEntidad<int>
{
    public int Id { get; set; }

    /// <summary>Codigo corto de uso interno, para etiquetar la instalacion (p. ej. "ADM").</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;

    // ---- Datos fiscales del emisor: figuran en cada documento fiscal ----

    /// <summary>Razón social completa (o nombre del contribuyente persona natural).</summary>
    public string RazonSocial { get; set; } = string.Empty;

    /// <summary>RIF con dígito verificador, normalizado ("J-12345678-9").</summary>
    public string Rif { get; set; } = string.Empty;

    public string DireccionFiscal { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;

    /// <summary>
    /// Contribuyente especial designado por el SENIAT. Decide dos cosas: si se percibe el IGTF
    /// (3 % sobre lo cobrado en divisas) y si el libro de ventas se lleva por quincena.
    /// </summary>
    public bool ContribuyenteEspecial { get; set; }

    /// <summary>Porcentajes de IVA vigentes. Deben coincidir con los programados en el emisor fiscal.</summary>
    public decimal IvaGeneral { get; set; } = 16m;
    public decimal IvaReducida { get; set; } = 8m;

    /// <summary>General + adicional de bienes suntuarios, ya sumados (16 + 15).</summary>
    public decimal IvaAdicional { get; set; } = 31m;

    /// <summary>Porcentaje del IGTF sobre pagos en divisas. Solo aplica si es contribuyente especial.</summary>
    public decimal IgtfPorcentaje { get; set; } = 3m;

    /// <summary>Si ya están los datos mínimos para emitir un documento fiscal.</summary>
    public bool DatosFiscalesCompletos =>
        !string.IsNullOrWhiteSpace(RazonSocial)
        && Services.Rif.EsRifValido(Rif)
        && !string.IsNullOrWhiteSpace(DireccionFiscal);

    public string Etiqueta => $"{Codigo} · {Nombre}";
    public string EstadoTexto => Activa ? "Activa" : "Inactiva";

    public Organizacion Clonar() => (Organizacion)MemberwiseClone();
}
