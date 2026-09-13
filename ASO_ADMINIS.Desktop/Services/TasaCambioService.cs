using System.Linq;
using System.Threading.Tasks;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// La tasa de cambio vigente y cómo se refresca desde la fuente oficial.
/// </summary>
public sealed class TasaCambioService
{
    private readonly ITasaCambioDataSource _tasas;
    private readonly ITasaCambioProveedor _proveedor;

    public TasaCambioService(ITasaCambioDataSource tasas, ITasaCambioProveedor proveedor)
    {
        _tasas = tasas;
        _proveedor = proveedor;
    }

    /// <summary>La última tasa guardada, o null si nunca se pudo traer ninguna.</summary>
    public TasaCambio? Actual() => _tasas.GetUltima();

    /// <summary>
    /// Trae la tasa de hoy de la fuente oficial y la guarda: "buscar o crear" por fecha, mismo
    /// criterio que el resto del scaffold. No atrapa el fallo del proveedor — lo decide quien
    /// llame (silencio en el arranque, diálogo en el botón manual de Administración).
    /// </summary>
    public async Task<TasaCambio> ActualizarDesdeApi()
    {
        var (valor, fecha) = await _proveedor.ObtenerTasaOficial();

        var existente = _tasas.GetAll().FirstOrDefault(t => t.Fecha == fecha);
        if (existente is { } actual)
        {
            actual.Valor = valor;
            _tasas.Update(actual);
            return actual;
        }

        return _tasas.Add(new TasaCambio { Fecha = fecha, Valor = valor });
    }
}
