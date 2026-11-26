using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

public interface ICuentaPorCobrarDataSource : ICrudDataSource<CuentaPorCobrar, int>
{
    CuentaPorCobrar? GetByVenta(int ventaId);
}
