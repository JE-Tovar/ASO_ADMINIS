using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Documentos fiscales de venta. <c>Update</c> y <c>Delete</c> lanzan: un documento emitido
/// es inalterable. Lo único que cambia después de emitir tiene su método propio.
/// </summary>
public interface IVentaDataSource : ICrudDataSource<Venta, int>
{
    /// <summary>Un documento de contingencia ya transmitido al emisor fiscal.</summary>
    void MarcarTransmitido(int id, DateTime cuando);

    /// <summary>Estampa el reporte Z en los documentos que cerró (solo los que aún no tenían).</summary>
    void EstamparZ(IReadOnlyCollection<int> ids, int numeroZ);
}
