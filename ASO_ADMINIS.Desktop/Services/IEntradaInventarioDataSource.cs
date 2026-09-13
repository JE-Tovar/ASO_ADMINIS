using System.Collections.Generic;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Entradas al almacén con sus líneas. La implementa una fuente EF Core; la interfaz mantiene la
/// UI y los ViewModels ajenos a la persistencia.
/// </summary>
public interface IEntradaInventarioDataSource : ICrudDataSource<EntradaInventario, int>
{
    /// <summary>Las entradas que generaron una cuenta por pagar concreta. Es el camino inverso
    /// del enlace suelto <see cref="EntradaInventario.FacturaProveedorId"/>.</summary>
    IEnumerable<EntradaInventario> GetByFacturaProveedor(int facturaProveedorId);
}
