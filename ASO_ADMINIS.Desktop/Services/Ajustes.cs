using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Las preferencias vivas del proceso, una sola copia.
///
/// Es estatica como <see cref="SesionActual"/> y por el mismo motivo: quien las lee lo hace
/// desde sitios muy dispersos (el arranque, el login, un servicio de dominio) y ninguno de esos
/// tiene un constructor donde inyectarlas. La pantalla de Configuracion si recibe el
/// <see cref="IAjustesStore"/> por constructor, que es lo que la hace comprobable.
/// </summary>
public static class Ajustes
{
    private static AjustesApp? _actual;

    /// <summary>
    /// Avisa de que los ajustes cambiaron. Lo escucha la ventana principal para reaplicar la
    /// escala: la pantalla de Configuracion no tiene forma de alcanzarla, y no deberia — es un
    /// ViewModel, no sabe que existe una ventana.
    /// </summary>
    public static event System.Action? Cambiaron;

    /// <summary>Se leen del disco la primera vez que alguien pregunta, no en un inicializador.</summary>
    public static AjustesApp Actual => _actual ??= DataSourceFactory.CrearAjustesStore().Leer();

    /// <returns><c>true</c> si se pudo escribir en el disco.</returns>
    public static bool Guardar()
    {
        var guardado = DataSourceFactory.CrearAjustesStore().Guardar(Actual);
        Cambiaron?.Invoke();
        return guardado;
    }

    /// <summary>
    /// Reemplaza los ajustes vivos y los persiste. Lo usa la pantalla de Configuracion al
    /// guardar, para que el resto de la aplicacion vea el cambio sin releer el archivo.
    /// </summary>
    public static bool Reemplazar(AjustesApp ajustes)
    {
        _actual = ajustes;
        return Guardar();
    }
}
