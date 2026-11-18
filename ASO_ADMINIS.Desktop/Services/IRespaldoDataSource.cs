using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>Historial de respaldos (tabla Respaldos) y las dos formas de hacerlos.</summary>
public interface IRespaldoDataSource
{
    RegistroRespaldo Anotar(RegistroRespaldo registro);

    IReadOnlyList<RegistroRespaldo> Ultimos(int cuantos);

    /// <summary>Fecha del último respaldo exitoso de ese tipo, o null.</summary>
    DateTime? UltimoExitoso(TipoRespaldo tipo);

    /// <summary>
    /// Copia nativa de la base a <paramref name="carpetaServidor"/> (ruta vista desde el servidor
    /// de SQL) y la verifica. Devuelve la ruta y el tamaño si el archivo es accesible desde aquí.
    /// </summary>
    (string Ruta, long Tamano, bool Verificado) RespaldoNativo(string carpetaServidor, string nombreArchivo);

    /// <summary>Exporta todas las tablas (todas las organizaciones) a un zip local.</summary>
    (string Ruta, long Tamano) ExportacionPortable(string rutaZip, string versionApp);

    /// <summary>Dónde deja el .bak: la carpeta configurada o, si no hay, la que usa SQL Server por defecto.</summary>
    string CarpetaServidor(string? configurada, string carpetaLocal);

    /// <summary>¿La base está en esta misma PC? Solo entonces se pueden limpiar los .bak viejos desde aquí.</summary>
    bool ServidorEsLocal();

    /// <summary>
    /// Toma un candado de sesión para que dos PCs no respalden a la vez. Null si otra lo tiene:
    /// entonces no se respalda. Liberarlo = desecharlo.
    /// </summary>
    IDisposable? TomarCandadoDeRespaldo();
}
