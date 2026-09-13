using System.Collections.Generic;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Asientos del libro de banco. La implementa una fuente EF Core; la interfaz mantiene la UI y
/// los ViewModels ajenos a la persistencia.
/// </summary>
public interface IMovimientoBancoDataSource : ICrudDataSource<MovimientoBanco, int>
{
    IEnumerable<MovimientoBanco> GetByCuenta(int cuentaId);

    /// <summary>
    /// Los asientos que ya nacieron de un documento. Es la consulta que sostiene el
    /// anti-doble-asiento: antes de escribir el cobro o el pago se comprueba que ese documento no
    /// tenga ya el suyo.
    /// </summary>
    IEnumerable<MovimientoBanco> GetByOrigen(OrigenMovimiento origen, int origenId);
}
