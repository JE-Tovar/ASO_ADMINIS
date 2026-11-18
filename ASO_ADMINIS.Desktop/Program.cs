using Velopack;

namespace ASO_ADMINIS.Desktop;

/// <summary>
/// Punto de entrada. Existe solo para que Velopack corra antes que WPF: cuando el instalador o
/// una actualizacion lanzan el .exe con sus argumentos (--veloapp-install, --veloapp-updated...),
/// <c>Run()</c> hace lo suyo y cierra el proceso ahi mismo, sin llegar a abrir el login.
/// En dotnet run / F5 no hace nada.
/// </summary>
public static class Program
{
    private static int RespaldoProgramado()
    {
        try
        {
            Services.Registro.Info("Respaldo programado (--respaldar)");
            var resultado = Configuration.DataSourceFactory.CrearServicioRespaldos()
                .Respaldar(Models.OrigenRespaldo.Programado, "(programado)");
            Services.Registro.Info("Respaldo programado terminado: {Resumen}", resultado.Resumen);
            return resultado.TodoBien ? 0 : 1;
        }
        catch (Exception ex)
        {
            Services.Registro.Fatal(ex, "No se pudo hacer el respaldo programado");
            return 2;
        }
    }

    [STAThread]
    public static void Main()
    {
        VelopackApp.Build().Run();

        Services.Registro.Iniciar();

        // Modo sin ventanas para el Programador de tareas del servidor (INSTALACION.md, Respaldo):
        //   ASO_ADMINIS.Desktop.exe --respaldar
        // Sale con 0 si los dos respaldos salieron bien, 1 si alguno falló, 2 si no se pudo ni empezar.
        if (Environment.GetCommandLineArgs().Skip(1).Any(a => string.Equals(a, "--respaldar", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = RespaldoProgramado();
            Services.Registro.Cerrar();
            return;
        }

        try
        {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            Services.Registro.Fatal(ex, "La aplicación terminó por un error no controlado");
            throw;
        }
        finally
        {
            Services.Registro.Cerrar();
        }
    }
}
