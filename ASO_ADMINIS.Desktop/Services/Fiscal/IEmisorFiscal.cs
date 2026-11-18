using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services.Fiscal;

/// <summary>
/// Quien da validez fiscal a un documento: una máquina fiscal o una imprenta digital
/// (Providencia SNAT/2026/00093). El software NO se asigna el número de control: lo devuelve
/// el emisor.
///
/// Todo lo fiscal de Ventas habla con esta interfaz, así que cambiar de medio de emisión es
/// escribir otra implementación y elegirla en <c>appsettings.local.json</c>
/// (<c>"EmisorFiscal": { "Tipo": ... }</c>, ver <see cref="EmisoresFiscales"/>). Hoy existe
/// solo <see cref="EmisorFiscalSimulado"/>; el conector real (p. ej. una máquina HKA o la API
/// de una imprenta digital) se escribe cuando el cliente elija equipo o proveedor.
/// </summary>
public interface IEmisorFiscal
{
    /// <summary>Nombre para mostrar: "Simulador (máquina fiscal)", "Imprenta digital X"…</summary>
    string Nombre { get; }

    /// <summary>True si es una máquina fiscal: tiene memoria fiscal y reportes X y Z.</summary>
    bool TieneReportesXZ { get; }

    /// <summary>False si es un simulador: lo que emite NO tiene valor fiscal.</summary>
    bool EsFiscal { get; }

    /// <summary>
    /// Emite el documento. Dos maneras de fallar, y el conector TIENE que distinguirlas:
    /// <list type="bullet">
    ///   <item><see cref="EmisorFiscalException"/>: seguro que NO se emitió (sin conexión antes de
    ///         enviar, sin papel, rechazado). El documento no se guarda y se puede reintentar o ir a
    ///         contingencia.</item>
    ///   <item><see cref="EmisionInciertaException"/>: se envió pero no se sabe si quedó (se cortó la
    ///         comunicación o venció el tiempo esperando la respuesta). NO se reintenta: podría
    ///         duplicar el documento. Va al diario fiscal para verificarlo en el equipo o con el
    ///         proveedor.</item>
    /// </list>
    /// Ante la duda, un conector debe lanzar la incierta, nunca la segura.
    /// </summary>
    ResultadoFiscal Emitir(Venta documento, Organizacion emisor);

    /// <summary>
    /// Comprueba que el emisor responde, sin emitir nada (estado de la máquina, o un "ping" a la API
    /// del proveedor). No lanza: el resultado lo dice.
    /// </summary>
    EstadoEmisor ProbarConexion();

    /// <summary>
    /// Registra en el emisor un documento que se hizo en papel de contingencia (art. de
    /// contingencias de la 00093). Conserva su número y control de papel.
    /// </summary>
    void TransmitirContingencia(Venta documento, Organizacion emisor);

    /// <summary>Lectura del turno sin cerrar (reporte X). Solo si <see cref="TieneReportesXZ"/>.</summary>
    ResumenFiscal ReporteX(string caja);

    /// <summary>Cierre diario (reporte Z): pone a cero los acumulados del día. Solo si <see cref="TieneReportesXZ"/>.</summary>
    ResumenFiscal ReporteZ(string caja);
}

/// <summary>Lo que devuelve el emisor por un documento emitido.</summary>
public sealed record ResultadoFiscal(
    string NumeroDocumento,
    string NumeroControl,
    string SerialEmisor,
    DateTime FechaHora,
    string? RutaDocumento);

/// <summary>Totales acumulados por el emisor (reporte X o Z).</summary>
public sealed record ResumenFiscal(
    int NumeroZ,
    DateTime FechaHora,
    int Documentos,
    decimal Exento,
    decimal BaseGeneral,
    decimal IvaGeneral,
    decimal BaseReducida,
    decimal IvaReducida,
    decimal BaseAdicional,
    decimal IvaAdicional,
    decimal Igtf,
    decimal Total);

/// <summary>El emisor no pudo emitir, y es SEGURO que no se emitió. El mensaje es para el usuario.</summary>
public sealed class EmisorFiscalException(string mensaje, Exception? interna = null)
    : Exception(mensaje, interna);

/// <summary>
/// No se sabe si el documento se emitió: se envió y no llegó respuesta. No se reintenta (podría
/// quedar duplicado); se verifica en el equipo o con el proveedor. El mensaje es para el usuario.
/// </summary>
public sealed class EmisionInciertaException(string mensaje, Exception? interna = null)
    : Exception(mensaje, interna);

/// <summary>Lo que dice <see cref="IEmisorFiscal.ProbarConexion"/>.</summary>
public sealed record EstadoEmisor(bool Disponible, string Detalle, string? Serial = null);
