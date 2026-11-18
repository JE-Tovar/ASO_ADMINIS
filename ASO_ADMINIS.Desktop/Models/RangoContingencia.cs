namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Un talonario de formatos de contingencia: los números de control que una imprenta autorizada
/// imprimió para esta organización ("00-000001" al "00-000500"). Cuando el emisor fiscal no
/// responde, la factura se hace en uno de esos formatos, y su número de control TIENE que estar
/// en un rango activo y no estar usado (ver <c>Services/RangosContingencia.cs</c>).
///
/// El número se escribe <see cref="Prefijo"/> + el correlativo con <see cref="Digitos"/> cifras.
/// No se borra, se desactiva: las facturas en contingencia lo referencian por su número.
/// </summary>
public class RangoContingencia : IEntidad<int>, IDeOrganizacion, IConVersion
{
    public byte[]? Version { get; set; }

    public int Id { get; set; }
    public int OrganizacionId { get; set; }

    /// <summary>Lo que va antes del correlativo, tal como está impreso ("00-"). Puede ser vacío.</summary>
    public string Prefijo { get; set; } = string.Empty;

    public int Desde { get; set; }
    public int Hasta { get; set; }

    /// <summary>Cifras del correlativo, con ceros a la izquierda (6 → "000123").</summary>
    public int Digitos { get; set; } = 6;

    // ---- La autorización ----
    public string Imprenta { get; set; } = string.Empty;
    public string ImprentaRif { get; set; } = string.Empty;

    /// <summary>Providencia o número de autorización que figura en el formato.</summary>
    public string Autorizacion { get; set; } = string.Empty;
    public DateTime? FechaAutorizacion { get; set; }

    public bool Activo { get; set; } = true;

    // ---- Uso (no se persiste: lo llena la fuente de datos contando las facturas) ----
    public int Usados { get; set; }

    // ---- Derivados ----
    public int Total => Hasta - Desde + 1;
    public int Quedan => Math.Max(0, Total - Usados);

    public string Numero(int correlativo) => Prefijo + correlativo.ToString("D" + Digitos);
    public string RangoTexto => $"{Numero(Desde)} al {Numero(Hasta)}";
    public string UsoTexto => $"{Usados} de {Total} usados · quedan {Quedan}";
    public string EstadoTexto => !Activo ? "Inactivo" : Quedan == 0 ? "Agotado" : "Activo";

    public RangoContingencia Clonar() => (RangoContingencia)MemberwiseClone();
}
