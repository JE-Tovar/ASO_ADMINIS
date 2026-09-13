using System;
using System.Threading.Tasks;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// De dónde sale la tasa de cambio oficial, detrás de una interfaz: <see cref="TasaCambioService"/>
/// no sabe que esto es una llamada HTTP a un servicio externo (ver <see cref="DolarApiTasaCambioProveedor"/>).
/// </summary>
public interface ITasaCambioProveedor
{
    /// <summary>Bolívares por dólar y la fecha a la que corresponde, según la fuente oficial (BCV).</summary>
    Task<(decimal Valor, DateTime Fecha)> ObtenerTasaOficial();
}
