using System.IO;
using System.Reflection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Registro técnico de la aplicación: un archivo por día en
/// <c>%LocalAppData%\ASO Adminis\logs\</c>, 30 días. Es para soporte, no para el negocio — lo que
/// cambió quién y cuándo va a la bitácora de la base (<c>RegistroAuditoria</c>).
///
/// Es local y no en la base a propósito: lo que más interesa registrar es justo cuando la base
/// no responde.
///
/// <b>Nunca</b> se registran contraseñas ni cadenas de conexión. Los mensajes son plantillas de
/// Serilog (<c>"Login de {Usuario}"</c>), así que los datos van como propiedades, no pegados.
///
/// Si no se llamó a <see cref="Iniciar"/> (pruebas), todo es un no-op: el logger por defecto de
/// Serilog no escribe nada.
/// </summary>
public static class Registro
{
    /// <summary>
    /// Misma carpeta de la máquina que <c>AppConfig.CarpetaMaquina</c>, calculada aquí sin pasar
    /// por AppConfig: leer la configuración puede fallar, y eso también hay que poder registrarlo.
    /// </summary>
    public static string Carpeta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ASO Adminis", "logs");

    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

    public static void Iniciar()
    {
        try
        {
            Directory.CreateDirectory(Carpeta);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.With(new DatosDeSesion())
                .Enrich.WithProperty("Version", Version)
                .Enrich.WithProperty("Equipo", Environment.MachineName)
                .WriteTo.File(
                    Path.Combine(Carpeta, "aso-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] v{Version} {Equipo} {Usuario} {Caja} | {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }
        catch
        {
            // Sin carpeta de logs la aplicación sigue: registrar no puede ser lo que la tumbe.
        }
    }

    public static void Cerrar() => Log.CloseAndFlush();

    public static void Info(string plantilla, params object?[] datos) => Log.Information(plantilla, datos);

    public static void Advertencia(string plantilla, params object?[] datos) => Log.Warning(plantilla, datos);

    public static void Advertencia(Exception ex, string plantilla, params object?[] datos) => Log.Warning(ex, plantilla, datos);

    public static void Error(Exception ex, string plantilla, params object?[] datos) => Log.Error(ex, plantilla, datos);

    public static void Fatal(Exception ex, string plantilla, params object?[] datos) => Log.Fatal(ex, plantilla, datos);

    /// <summary>Quién y en qué caja, leído en el momento de cada línea (la sesión cambia).</summary>
    private sealed class DatosDeSesion : ILogEventEnricher
    {
        public void Enrich(LogEvent e, ILogEventPropertyFactory f)
        {
            var sesion = SesionActual.Instancia;
            e.AddPropertyIfAbsent(f.CreateProperty("Usuario", sesion.UsuarioActual?.NombreUsuario ?? "-"));
            e.AddPropertyIfAbsent(f.CreateProperty("Caja", sesion.CajaActual?.Codigo ?? "-"));
        }
    }
}
