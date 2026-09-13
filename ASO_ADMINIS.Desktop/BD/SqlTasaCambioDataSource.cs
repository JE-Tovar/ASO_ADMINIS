using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

public class SqlTasaCambioDataSource : SqlCrudDataSource<TasaCambio, int>, ITasaCambioDataSource
{
    public TasaCambio? GetUltima() =>
        Consultar(q => q.OrderByDescending(t => t.Fecha)).FirstOrDefault();
}
