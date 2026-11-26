using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

public class SqlMovimientoCuentaPorCobrarDataSource : SqlCrudDataSource<MovimientoCuentaPorCobrar, int>, IMovimientoCuentaPorCobrarDataSource
{
    public IEnumerable<MovimientoCuentaPorCobrar> GetByCuenta(int cuentaPorCobrarId) =>
        Consultar(q => q.Where(m => m.CuentaPorCobrarId == cuentaPorCobrarId).OrderBy(m => m.Fecha).ThenBy(m => m.Id));
}
