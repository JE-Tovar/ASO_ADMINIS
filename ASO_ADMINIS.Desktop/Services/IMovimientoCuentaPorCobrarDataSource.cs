using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

public interface IMovimientoCuentaPorCobrarDataSource : ICrudDataSource<MovimientoCuentaPorCobrar, int>
{
    IEnumerable<MovimientoCuentaPorCobrar> GetByCuenta(int cuentaPorCobrarId);
}
