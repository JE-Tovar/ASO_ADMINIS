using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

/// <summary>
/// Filtra en el servidor, no en memoria: la bitácora crece con cada guardado y traerla entera
/// para filtrarla aquí sería lento justo cuando más se necesita.
/// </summary>
public class SqlBitacoraDataSource : IBitacoraDataSource
{
    public IReadOnlyList<RegistroAuditoria> Consultar(DateTime desde, DateTime hasta, string? texto, string? entidad, int maximo)
    {
        using var context = new AsoAdminisDbContext();
        var fin = hasta.Date.AddDays(1);
        var q = context.RegistrosAuditoria.Where(r => r.Fecha >= desde.Date && r.Fecha < fin);

        if (!string.IsNullOrWhiteSpace(entidad))
            q = q.Where(r => r.Entidad == entidad);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim();
            q = q.Where(r => r.UsuarioNombre.Contains(t) || r.Resumen.Contains(t)
                             || r.EntidadId == t || r.CajaCodigo.Contains(t) || r.Equipo.Contains(t));
        }

        return q.OrderByDescending(r => r.Fecha).ThenByDescending(r => r.Id).Take(maximo).ToList();
    }

    public IReadOnlyList<string> Entidades()
    {
        using var context = new AsoAdminisDbContext();
        return context.RegistrosAuditoria.Select(r => r.Entidad).Distinct().OrderBy(e => e).ToList();
    }
}
