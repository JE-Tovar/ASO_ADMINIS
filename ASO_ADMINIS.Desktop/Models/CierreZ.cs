namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Un reporte Z (cierre diario de una máquina fiscal): lo que acumuló la máquina en su memoria
/// fiscal y, al lado, lo que el sistema tiene registrado para los mismos documentos. Si no
/// coinciden hay algo que revisar (un documento emitido y no guardado, o al revés).
///
/// Con una imprenta digital no hay Z: el cierre del día es solo el resumen diario que calcula
/// el sistema (ver <c>CierreService.ResumenDiario</c>).
/// </summary>
public class CierreZ : IEntidad<int>, IDeOrganizacion
{
    public int OrganizacionId { get; set; }
    public int Id { get; set; }

    public int NumeroZ { get; set; }
    public string CajaCodigo { get; set; } = string.Empty;
    public string SerialEmisor { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }

    // ---- Lo que dice la máquina ----
    public int Documentos { get; set; }
    public decimal Exento { get; set; }
    public decimal BaseGeneral { get; set; }
    public decimal IvaGeneral { get; set; }
    public decimal BaseReducida { get; set; }
    public decimal IvaReducida { get; set; }
    public decimal BaseAdicional { get; set; }
    public decimal IvaAdicional { get; set; }
    public decimal Igtf { get; set; }
    public decimal Total { get; set; }

    // ---- Lo que dice el sistema para los mismos documentos ----
    public int DocumentosSistema { get; set; }
    public decimal TotalSistema { get; set; }

    public int CreadoPorId { get; set; }
    public string CreadoPorNombre { get; set; } = string.Empty;

    public decimal Diferencia => Total - TotalSistema;
    public bool Cuadra => Math.Abs(Diferencia) < 0.01m && Documentos == DocumentosSistema;

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy HH:mm");
    public string TotalTexto => Total.ToString("N2");
    public string TotalSistemaTexto => TotalSistema.ToString("N2");
    public string CuadraTexto => Cuadra ? "Cuadra" : $"Diferencia {Diferencia:N2}";

    public CierreZ Clonar() => (CierreZ)MemberwiseClone();
}
