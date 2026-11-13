using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

/// <summary>Catálogo de artículos (bienes y servicios), ordenado por código.</summary>
public class SqlArticuloDataSource : SqlCrudDataSource<Articulo, int>, IArticuloDataSource
{
    protected override IQueryable<Articulo> Ordenar(IQueryable<Articulo> consulta)
        => consulta.OrderBy(a => a.Codigo);

    public IEnumerable<Articulo> GetActivos()
        => Consultar(q => q.Where(a => a.Activo).OrderBy(a => a.Codigo));
}
