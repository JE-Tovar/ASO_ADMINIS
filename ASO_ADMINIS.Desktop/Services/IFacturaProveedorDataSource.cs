using System.Collections.Generic;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Facturas de compra (Cuentas por Pagar). La implementa
/// una fuente EF Core; la interfaz mantiene la UI y los ViewModels ajenos a la persistencia.
/// </summary>
public interface IFacturaProveedorDataSource : ICrudDataSource<FacturaProveedor, int>
{
    IEnumerable<FacturaProveedor> GetByProveedor(int proveedorId);
}
