using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

/// <summary>El boleto de salida se guarda entero con sus líneas.</summary>
public class SqlSalidaInventarioDataSource
    : SqlAgregadoDataSource<SalidaInventario, int>, ISalidaInventarioDataSource
{
    protected override IQueryable<SalidaInventario> Incluir(IQueryable<SalidaInventario> consulta)
        => consulta.Include(s => s.Lineas);

    /// <summary>Lo más reciente arriba: el historial se lee de vuelta desde hoy.</summary>
    protected override IQueryable<SalidaInventario> Ordenar(IQueryable<SalidaInventario> consulta)
        => consulta.OrderByDescending(s => s.Fecha).ThenByDescending(s => s.Id);

    protected override Expression<Func<SalidaInventario, bool>> PorId(int id) => s => s.Id == id;

    protected override IEnumerable<object> HijosDe(SalidaInventario raiz) => raiz.Lineas;

    protected override void CopiarHijos(SalidaInventario destino, SalidaInventario origen)
        => destino.Lineas = origen.Lineas;
}
