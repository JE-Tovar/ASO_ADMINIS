using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>Lectura de la bitácora de auditoría. No hay escritura: la llena sola AsoAdminisDbContext.</summary>
public interface IBitacoraDataSource
{
    /// <summary>
    /// Lo registrado entre <paramref name="desde"/> y <paramref name="hasta"/> (ambos días
    /// incluidos), de lo más nuevo a lo más viejo, como mucho <paramref name="maximo"/> filas.
    /// </summary>
    IReadOnlyList<RegistroAuditoria> Consultar(DateTime desde, DateTime hasta, string? texto, string? entidad, int maximo);

    /// <summary>Los tipos de registro que aparecen en la bitácora, para el filtro.</summary>
    IReadOnlyList<string> Entidades();
}
