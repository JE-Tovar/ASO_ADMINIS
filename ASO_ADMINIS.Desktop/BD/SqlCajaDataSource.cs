using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

public class SqlCajaDataSource : SqlCrudDataSource<Caja, int>, ICajaDataSource
{
    protected override IQueryable<Caja> Ordenar(IQueryable<Caja> consulta)
        => consulta.OrderBy(c => c.Codigo);
}
