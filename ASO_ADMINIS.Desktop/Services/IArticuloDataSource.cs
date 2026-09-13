using System.Collections.Generic;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Catálogo de artículos del almacén. La implementa una fuente EF Core; la interfaz mantiene la
/// UI y los ViewModels ajenos a la persistencia.
/// </summary>
public interface IArticuloDataSource : ICrudDataSource<Articulo, int>
{
    /// <summary>Lo que se puede elegir hoy en una entrada o una salida: los artículos dados de
    /// baja siguen en el historial pero no se ofrecen en los formularios.</summary>
    IEnumerable<Articulo> GetActivos();
}
