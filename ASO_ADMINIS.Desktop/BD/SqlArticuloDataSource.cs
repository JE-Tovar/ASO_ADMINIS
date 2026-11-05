using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

/// <summary>El artículo se guarda entero con sus presentaciones (galón, cuñete…).</summary>
public class SqlArticuloDataSource : SqlAgregadoDataSource<Articulo, int>, IArticuloDataSource
{
    protected override IQueryable<Articulo> Incluir(IQueryable<Articulo> consulta)
        => consulta.Include(a => a.Presentaciones);

    protected override IQueryable<Articulo> Ordenar(IQueryable<Articulo> consulta)
        => consulta.OrderBy(a => a.Codigo);

    protected override Expression<Func<Articulo, bool>> PorId(int id) => a => a.Id == id;

    protected override IEnumerable<object> HijosDe(Articulo raiz) => raiz.Presentaciones;

    protected override void CopiarHijos(Articulo destino, Articulo origen)
        => destino.Presentaciones = origen.Presentaciones;

    public IEnumerable<Articulo> GetActivos()
        => Consultar(q => Incluir(q).Where(a => a.Activo).OrderBy(a => a.Codigo));
}
