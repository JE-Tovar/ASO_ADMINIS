using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>Talonarios de contingencia de la organización activa (Administración · Contingencia).</summary>
public interface IRangoContingenciaDataSource : ICrudDataSource<RangoContingencia, int>
{
    /// <summary>Todos los números de control ya puestos en documentos de venta.</summary>
    IReadOnlyList<string> NumerosDeControlUsados();
}
