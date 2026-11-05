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

        // Instalada, el appsettings.local.json no viaja en el paquete (ver el .csproj) y va en la
        // carpeta de la maquina; en desarrollo sigue junto al .exe, copiado desde el proyecto.
        var local = Instalada
            ? Path.Combine(CarpetaMaquina, "appsettings.local.json")
            : Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile(local, optional: true, reloadOnChange: false)
            .Build();
    }

    /// <summary>
    /// ¿Corre desde una instalacion de Velopack? Su layout es <c>%LocalAppData%\AsoAdminis\current\</c>
    /// con <c>Update.exe</c> un nivel arriba. Se mira el disco y no el runtime de Velopack porque
    /// esto se evalua en el primer acceso a la configuracion, y asi no depende del orden de arranque.
    /// </summary>
    private static bool Instalada =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "..", "Update.exe"));

    /// <summary>
    /// Lo que es de esta maquina y tiene que sobrevivir a las actualizaciones: Velopack reemplaza
    /// la carpeta <c>current\</c> entera en cada una, y al desinstalar borra la suya completa.
    /// </summary>
    private static string CarpetaMaquina =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ASO Adminis");

    /// <summary>
    /// Carpeta del archivo LocalDB. En desarrollo, App_Data junto al proyecto (no dentro de bin):
    /// asume el layout de "dotnet run" / F5 en Debug, bin/Debug/netX.0-windows tres niveles bajo
    /// ASO_ADMINIS.Desktop/. Instalada, dentro de <see cref="CarpetaMaquina"/>.
    /// </summary>
    private static string CarpetaDatos =>
        Instalada
            ? Path.Combine(CarpetaMaquina, "App_Data")
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "App_Data"));

    /// <summary>Un valor suelto de la configuracion ("Seccion:Clave"), o null si no esta.</summary>
    public static string? Valor(string clave) => _config[clave];

    /// <summary>Cadena de conexion a SQL Server (clave ConnectionStrings:AsoAdminisDb).</summary>
    public static string ConnectionString =>
        _config.GetConnectionString("AsoAdminisDb")
        ?? throw new InvalidOperationException(
            "No se encontro la cadena de conexion 'AsoAdminisDb'. " +
            "Revisa appsettings.json o crea appsettings.local.json a partir de appsettings.local.example.json.");
}
