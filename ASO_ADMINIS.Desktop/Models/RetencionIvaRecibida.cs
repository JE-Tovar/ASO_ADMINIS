namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Comprobante de retención de IVA que entregó un cliente contribuyente especial (agente de
/// retención): se quedó con el 75 % o el 100 % del IVA de una factura y lo enteró al fisco.
///
/// Es una fila aparte y no un campo de la factura porque la factura es inalterable y el
/// comprobante llega después (a veces en otro período). Va al libro de ventas: "IVA retenido
/// por el comprador" y su número de comprobante.
/// </summary>
public class RetencionIvaRecibida : IEntidad<int>, IDeOrganizacion
{
    public int OrganizacionId { get; set; }
    public int Id { get; set; }

    public int VentaId { get; set; }
    public string VentaNumeroFiscal { get; set; } = string.Empty;  // snapshot
    public string ClienteRif { get; set; } = string.Empty;         // snapshot (el agente)
    public string ClienteNombre { get; set; } = string.Empty;      // snapshot

    /// <summary>N° del comprobante (AAAAMM + correlativo de 8 del agente: 14 dígitos).</summary>
    public string NumeroComprobante { get; set; } = string.Empty;
    public DateTime FechaComprobante { get; set; }

    /// <summary>75 o 100.</summary>
    public decimal Porcentaje { get; set; }

    /// <summary>IVA de la factura sobre el que se retuvo (snapshot).</summary>
    public decimal IvaFactura { get; set; }

    public decimal MontoRetenido { get; set; }

    public int CreadoPorId { get; set; }
    public DateTime FechaCreacion { get; set; }

    public string FechaTexto => FechaComprobante.ToString("dd/MM/yyyy");
    public string MontoTexto => MontoRetenido.ToString("N2");

    public RetencionIvaRecibida Clonar() => (RetencionIvaRecibida)MemberwiseClone();
}
