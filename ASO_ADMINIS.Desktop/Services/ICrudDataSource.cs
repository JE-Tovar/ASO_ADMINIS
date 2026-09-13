using System.Collections.Generic;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Fuente de datos con operaciones CRUD para una entidad maestra. La implementan las clases
/// <c>Sql…DataSource</c> de <c>BD/</c>; la interfaz mantiene la UI y los ViewModels ajenos a la
/// persistencia.
///
/// Devuelve <c>IEnumerable</c> y no <c>IQueryable</c>, así que el filtrado por organización NO
/// puede vivir aquí: lo aplica EF con un filtro global (ver <c>BD/DbContext.cs</c>).
/// </summary>
public interface ICrudDataSource<T, TId> where T : IEntidad<TId>
{
    IEnumerable<T> GetAll();
    T? GetById(TId id);

    /// <returns>El ítem guardado, con el <c>Id</c> ya asignado.</returns>
    T Add(T item);

    void Update(T item);
    void Delete(TId id);
}
