using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

/// <summary>
/// Documentos fiscales de venta. Renglones y pagos son colecciones propias (<c>OwnsMany</c>),
/// así que vienen solos con la raíz.
///
/// Un documento emitido es inalterable (ver <c>AsoAdminisDbContext.ProtegerDocumentosFiscales</c>):
/// no hay <see cref="Update"/> ni <see cref="Delete"/>. Las dos únicas escrituras legítimas
/// después de emitir tienen su método, que carga la fila rastreada y toca solo su campo.
/// </summary>
public class SqlVentaDataSource : SqlCrudDataSource<Venta, int>, IVentaDataSource
{
    public override void Update(Venta item) =>
        throw new InvalidOperationException("Un documento fiscal no se modifica: se corrige con una nota de crédito o de débito.");

    public override void Delete(int id) =>
        throw new InvalidOperationException("Un documento fiscal no se elimina: se corrige con una nota de crédito.");

    public void MarcarTransmitido(int id, DateTime cuando)
    {
        using var context = new AsoAdminisDbContext();
        var venta = context.Ventas.First(v => v.Id == id);
        venta.Estado = EstadoVenta.Registrada;
        venta.FechaTransmision = cuando;
        context.SaveChanges();
    }

    public void EstamparZ(IReadOnlyCollection<int> ids, int numeroZ)
    {
        using var context = new AsoAdminisDbContext();
        foreach (var venta in context.Ventas.Where(v => ids.Contains(v.Id) && v.NumeroZ == null))
            venta.NumeroZ = numeroZ;
        context.SaveChanges();
    }
}
