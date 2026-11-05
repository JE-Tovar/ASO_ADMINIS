using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

public class SqlRetencionIvaDataSource : SqlCrudDataSource<RetencionIvaRecibida, int>, IRetencionIvaDataSource
{
}

/// <summary>Un Z es un registro fiscal: no se edita ni se borra.</summary>
public class SqlCierreZDataSource : SqlCrudDataSource<CierreZ, int>, ICierreZDataSource
{
    public override void Update(CierreZ item) =>
        throw new InvalidOperationException("Un reporte Z no se modifica.");

    public override void Delete(int id) =>
        throw new InvalidOperationException("Un reporte Z no se elimina.");
}
