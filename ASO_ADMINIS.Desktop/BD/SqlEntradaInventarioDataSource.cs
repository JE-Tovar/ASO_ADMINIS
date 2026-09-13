using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

/// <summary>La entrada se guarda entera con sus líneas.</summary>
public class SqlEntradaInventarioDataSource
    : SqlAgregadoDataSource<EntradaInventario, int>, IEntradaInventarioDataSource
{
    protected override IQueryable<EntradaInventario> Incluir(IQueryable<EntradaInventario> consulta)
        => consulta.Include(e => e.Lineas);

    /// <summary>Lo más reciente arriba: el historial se lee de vuelta desde hoy.</summary>
    protected override IQueryable<EntradaInventario> Ordenar(IQueryable<EntradaInventario> consulta)
        => consulta.OrderByDescending(e => e.Fecha).ThenByDescending(e => e.Id);

    protected override Expression<Func<EntradaInventario, bool>> PorId(int id) => e => e.Id == id;

    protected override IEnumerable<object> HijosDe(EntradaInventario raiz) => raiz.Lineas;

    protected override void CopiarHijos(EntradaInventario destino, EntradaInventario origen)
        => destino.Lineas = origen.Lineas;

    public IEnumerable<EntradaInventario> GetByFacturaProveedor(int facturaProveedorId)
        => Consultar(q => Incluir(q).Where(e => e.FacturaProveedorId == facturaProveedorId));
}
