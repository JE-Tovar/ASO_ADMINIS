using System.Linq;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

public class SqlTurnoDataSource : SqlCrudDataSource<Turno, int>, ITurnoDataSource
{
    protected override IQueryable<Turno> Ordenar(IQueryable<Turno> consulta)
        => consulta.OrderByDescending(t => t.Apertura);

    public Turno? AbiertoDe(int cajaId) =>
        Consultar(q => q.Where(t => t.CajaId == cajaId && t.Estado == EstadoTurno.Abierto)).FirstOrDefault();
}
