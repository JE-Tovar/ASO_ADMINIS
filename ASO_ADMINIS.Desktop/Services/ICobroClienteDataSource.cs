using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

public interface ICobroClienteDataSource : ICrudDataSource<CobroCliente, int>
{
    IEnumerable<CobroCliente> GetByCuenta(int cuentaPorCobrarId);
    IEnumerable<CobroCliente> GetByTurno(int turnoId);
}
