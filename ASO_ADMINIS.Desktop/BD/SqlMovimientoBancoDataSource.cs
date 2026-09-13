using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

public class SqlMovimientoBancoDataSource : SqlCrudDataSource<MovimientoBanco, int>, IMovimientoBancoDataSource
{
    /// <summary>
    /// Del más reciente al más viejo, que es como se lee un extracto. El saldo corrido lo calcula
    /// la pantalla; aquí solo importa que el orden sea estable.
    /// </summary>
    protected override IQueryable<MovimientoBanco> Ordenar(IQueryable<MovimientoBanco> consulta)
        => consulta.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id);

    public IEnumerable<MovimientoBanco> GetByCuenta(int cuentaId)
        => Consultar(q => q.Where(m => m.CuentaId == cuentaId)
                           .OrderByDescending(m => m.Fecha)
                           .ThenByDescending(m => m.Id));

    public IEnumerable<MovimientoBanco> GetByOrigen(OrigenMovimiento origen, int origenId)
        => Consultar(q => q.Where(m => m.Origen == origen && m.OrigenId == origenId));
}
