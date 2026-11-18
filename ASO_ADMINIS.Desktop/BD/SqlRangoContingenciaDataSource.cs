using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

public class SqlRangoContingenciaDataSource : SqlCrudDataSource<RangoContingencia, int>, IRangoContingenciaDataSource
{
    protected override IQueryable<RangoContingencia> Ordenar(IQueryable<RangoContingencia> consulta)
        => consulta.OrderBy(r => r.Prefijo).ThenBy(r => r.Desde);

    /// <summary>El listado lleva cuántos números de cada talonario ya se usaron.</summary>
    public override IEnumerable<RangoContingencia> GetAll()
    {
        var rangos = base.GetAll().ToList();
        var usados = NumerosDeControlUsados();
        foreach (var r in rangos)
            r.Usados = RangosContingencia.UsadosEn(r, usados);
        return rangos;
    }

    public IReadOnlyList<string> NumerosDeControlUsados()
    {
        using var context = new AsoAdminisDbContext();
        return context.Ventas.Where(v => v.NumeroControl != "").Select(v => v.NumeroControl).ToList();
    }
}
