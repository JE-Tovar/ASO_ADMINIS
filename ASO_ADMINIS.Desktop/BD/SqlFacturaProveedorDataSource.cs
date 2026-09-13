using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

/// <summary>La factura de proveedor se guarda entera con sus líneas.</summary>
public class SqlFacturaProveedorDataSource : SqlAgregadoDataSource<FacturaProveedor, int>, IFacturaProveedorDataSource
{
    protected override IQueryable<FacturaProveedor> Incluir(IQueryable<FacturaProveedor> consulta)
        => consulta.Include(f => f.Lineas);

    protected override Expression<Func<FacturaProveedor, bool>> PorId(int id) => f => f.Id == id;

    protected override IEnumerable<object> HijosDe(FacturaProveedor raiz) => raiz.Lineas;

    protected override void CopiarHijos(FacturaProveedor destino, FacturaProveedor origen)
        => destino.Lineas = origen.Lineas;

    public IEnumerable<FacturaProveedor> GetByProveedor(int proveedorId)
        => Consultar(q => q.Where(e => e.ProveedorId == proveedorId));
}
