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
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
