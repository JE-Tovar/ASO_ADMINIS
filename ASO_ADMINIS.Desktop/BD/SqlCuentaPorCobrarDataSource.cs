using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

public class SqlCuentaPorCobrarDataSource : SqlCrudDataSource<CuentaPorCobrar, int>, ICuentaPorCobrarDataSource
{
    protected override IQueryable<CuentaPorCobrar> Ordenar(IQueryable<CuentaPorCobrar> consulta) =>
        consulta.OrderByDescending(c => c.FechaEmision).ThenByDescending(c => c.Id);

    public CuentaPorCobrar? GetByVenta(int ventaId) =>
        Consultar(q => q.Where(c => c.VentaId == ventaId)).SingleOrDefault();
}
