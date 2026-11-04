using System;
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

    /// <summary>
    /// Avisa que se guardó una tasa nueva, venga del arranque o del botón de Administración.
    /// Estático porque cada pantalla crea su propio servicio; lo escucha el pie del sidebar.
    /// Puede llegar desde un hilo que no es el de interfaz.
    /// </summary>
    public static event Action<TasaCambio>? TasaActualizada;

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

        TasaCambio guardada;
        var existente = _tasas.GetAll().FirstOrDefault(t => t.Fecha == fecha);
        if (existente is { } actual)
        {
            actual.Valor = valor;
            _tasas.Update(actual);
            guardada = actual;
        }
        else
        {
            guardada = _tasas.Add(new TasaCambio { Fecha = fecha, Valor = valor });
        }

        TasaActualizada?.Invoke(guardada);
        return guardada;
    }
}
