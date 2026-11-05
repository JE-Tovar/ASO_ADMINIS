using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>Comprobantes de retención de IVA recibidos de clientes agentes de retención.</summary>
public interface IRetencionIvaDataSource : ICrudDataSource<RetencionIvaRecibida, int>
{
}

/// <summary>Reportes Z guardados (cierres diarios de la máquina fiscal).</summary>
public interface ICierreZDataSource : ICrudDataSource<CierreZ, int>
{
}
