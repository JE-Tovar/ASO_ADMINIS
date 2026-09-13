using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

public class SqlCuentaBancariaDataSource : SqlCrudDataSource<CuentaBancaria, int>, ICuentaBancariaDataSource
{
    protected override IQueryable<CuentaBancaria> Ordenar(IQueryable<CuentaBancaria> consulta)
        => consulta.OrderBy(c => c.Nombre);

    public IEnumerable<CuentaBancaria> GetActivas()
        => Consultar(q => q.Where(c => c.Activa).OrderBy(c => c.Nombre));
}
