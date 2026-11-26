using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

public class SqlCobroClienteDataSource : SqlCrudDataSource<CobroCliente, int>, ICobroClienteDataSource
{
    public IEnumerable<CobroCliente> GetByCuenta(int cuentaPorCobrarId) =>
        Consultar(q => q.Where(c => c.CuentaPorCobrarId == cuentaPorCobrarId).OrderBy(c => c.Fecha).ThenBy(c => c.Id));

    public IEnumerable<CobroCliente> GetByTurno(int turnoId) =>
        Consultar(q => q.Where(c => c.TurnoId == turnoId));
}
