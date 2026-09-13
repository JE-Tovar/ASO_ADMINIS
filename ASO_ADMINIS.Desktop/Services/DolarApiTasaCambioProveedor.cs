using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Tasa oficial BCV desde DolarAPI (<c>https://ve.dolarapi.com</c>), gratuita y sin clave.
/// Primera dependencia de red de esta app — cualquier fallo (sin internet, tiempo agotado,
/// respuesta inesperada) se propaga como excepción; no hay reintentos ni caché: quien llame
/// decide qué hacer (ver <see cref="TasaCambioService.ActualizarDesdeApi"/>).
/// </summary>
public sealed class DolarApiTasaCambioProveedor : ITasaCambioProveedor
{
    private const string Url = "https://ve.dolarapi.com/v1/dolares/oficial";

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<(decimal Valor, DateTime Fecha)> ObtenerTasaOficial()
    {
        var respuesta = await _http.GetFromJsonAsync<RespuestaDolarApi>(Url)
            ?? throw new InvalidOperationException("La respuesta de la tasa de cambio llegó vacía.");

        if (respuesta.Promedio is not { } valor || valor <= 0)
            throw new InvalidOperationException("La tasa de cambio recibida no es válida.");

        return (valor, respuesta.FechaActualizacion.Date);
    }

    private sealed record RespuestaDolarApi(
        [property: JsonPropertyName("promedio")] decimal? Promedio,
        [property: JsonPropertyName("fechaActualizacion")] DateTimeOffset FechaActualizacion);
}
