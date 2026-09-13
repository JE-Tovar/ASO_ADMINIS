using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace ASO_ADMINIS.Desktop.Configuration;

/// <summary>
/// Punto unico de acceso a la configuracion de la aplicacion.
/// Carga appsettings.json (valores por defecto, en el repo) y superpone
/// appsettings.local.json (por maquina, en .gitignore) si existe.
/// </summary>
public static class AppConfig
{
    private static readonly IConfigurationRoot _config = Build();

    private static IConfigurationRoot Build()
    {
        // La cadena por defecto usa LocalDB con |DataDirectory|, que SqlClient resuelve contra
        // esta ruta. Es un archivo por maquina (ver .gitignore): cada quien tiene el suyo, nadie
        // depende de la PC de otro para poder conectarse.
        Directory.CreateDirectory(CarpetaDatos);
        AppDomain.CurrentDomain.SetData("DataDirectory", CarpetaDatos);

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
            .Build();
    }

    /// <summary>
    /// Carpeta del archivo LocalDB (App_Data, junto al proyecto, no dentro de bin). Asume el
    /// layout de "dotnet run" / F5 en Debug (bin/Debug/netX.0-windows tres niveles bajo
    /// ASO_ADMINIS.Desktop/); si el dia de manana se empaqueta un instalador, esto hay que revisarlo.
    /// </summary>
    private static string CarpetaDatos =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "App_Data"));

    /// <summary>Cadena de conexion a SQL Server (clave ConnectionStrings:AsoAdminisDb).</summary>
    public static string ConnectionString =>
        _config.GetConnectionString("AsoAdminisDb")
        ?? throw new InvalidOperationException(
            "No se encontro la cadena de conexion 'AsoAdminisDb'. " +
            "Revisa appsettings.json o crea appsettings.local.json a partir de appsettings.local.example.json.");
}
