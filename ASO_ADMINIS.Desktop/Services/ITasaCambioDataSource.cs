using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

public interface ITasaCambioDataSource : ICrudDataSource<TasaCambio, int>
{
    /// <summary>La fila con la fecha más reciente, o null si nunca se guardó ninguna.</summary>
    TasaCambio? GetUltima();
}
