using System.Windows;
using ASO_ADMINIS.Desktop.BD;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Services;
using ASO_ADMINIS.Desktop.Views;
using Microsoft.EntityFrameworkCore;

namespace ASO_ADMINIS.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Registro.Info("Inicio de ASO Adminis {Version}", Registro.Version);
        InstalarManejoDeErrores();

        // Toda operación de dominio que escriba en más de una tabla pasa por aquí (ver
        // Services/UnidadDeTrabajo.cs). Las pruebas se quedan con la directa, sin SQL.
        UnidadDeTrabajo.Actual = new UnidadDeTrabajoSql();

        // El tema antes de abrir nada: si se aplicara despues, la pantalla de login parpadearia
        // en claro antes de pasar a oscuro.
        Tema.Aplicar(Ajustes.Actual.Tema);

        // El esquema, antes de la primera consulta: si la base se quedó atrás respecto al código
        // (lo normal tras un pull con migraciones nuevas), lo que se ve no es un error de esquema
        // sino la pantalla en blanco o la excepción del módulo que estrena tabla, disfrazada de
        // bug de ese módulo. Aquí se resuelve solo, y si no puede, se dice.
        if (!ActualizarEsquema())
        {
            Shutdown();
            return;
        }

        // La base de datos se toca ya en el arranque (para saber si hay usuarios), así que un
        // problema de conexión se ve aquí y no a mitad de una navegación, disfrazado de otra cosa.
        if (!HayUsuarios())
        {
            Shutdown();
            return;
        }

        var login = new LoginView();
        if (login.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        var main = new MainWindow();
        MainWindow = main;
        main.Show();

        // A partir de aquí sí queremos que cerrar la última ventana cierre la app.
        ShutdownMode = ShutdownMode.OnLastWindowClose;

        // Despues del login y con la ventana principal ya abierta: el aviso de "hay version
        // nueva" no debe tapar el login ni retrasar el arranque mientras se consulta GitHub.
        _ = Actualizaciones.BuscarAsync();
    }

    /// <summary>
    /// Lo que se escape de todo <c>try</c>. Antes cerraba la aplicación sin dejar rastro; ahora
    /// queda en el registro y, si fue en la interfaz, se avisa y se sigue: una excepción de base
    /// en un botón no tiene por qué tumbar la caja entera.
    /// </summary>
    private void InstalarManejoDeErrores()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Registro.Error(args.Exception, "Error no controlado en la interfaz");
            args.Handled = true;
            MessageBox.Show(
                $"Ocurrió un error inesperado y la acción no se completó.\n\n{args.Exception.Message}\n\n" +
                $"Quedó anotado en el registro de errores ({Registro.Carpeta}). " +
                "Si se repite, envíaselo a soporte.",
                "ASO Adminis", MessageBoxButton.OK, MessageBoxImage.Warning);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Registro.Fatal(ex, "Error no controlado (terminando: {Terminando})", args.IsTerminating);
            Registro.Cerrar();
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Registro.Error(args.Exception, "Error no observado en una tarea de fondo");
            args.SetObserved();
        };
    }

    /// <summary>
    /// Pone la base de datos al día con las migraciones del código, y la crea entera si no
    /// existía (primer arranque en una máquina nueva).
    ///
    /// Se consulta primero si hay algo pendiente en vez de llamar a <c>Migrate</c> a secas: en
    /// el caso normal —base ya al día— eso evita el bloqueo que <c>Migrate</c> toma para
    /// serializar migraciones, que es lo que haría que abrir dos instancias a la vez se
    /// esperasen entre sí sin motivo.
    /// </summary>
    private static bool ActualizarEsquema()
    {
        try
        {
            using var db = new AsoAdminisDbContext();
            var pendientes = db.Database.GetPendingMigrations().ToList();
            if (pendientes.Count > 0)
            {
                Registro.Info("Aplicando migraciones: {Migraciones}", string.Join(", ", pendientes));
                db.Database.Migrate();
            }

            return true;
        }
        catch (Exception ex)
        {
            Registro.Error(ex, "No se pudo actualizar el esquema de la base");
            // Seguir con el esquema viejo no es una opción: daría errores por todas partes
            // menos donde está la causa.
            MessageBox.Show(
                $"No se pudo actualizar la base de datos.\n\n{ex.Message}\n\n" +
                "Revisa la cadena de conexión en appsettings.local.json.",
                "ASO Adminis", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>
    /// ¿Hay con quién iniciar sesión? Si la base está vacía ofrece crear la organización y su
    /// usuario desarrollador; si no se puede consultar, lo dice y no sigue.
    /// </summary>
    private static bool HayUsuarios()
    {
        try
        {
            if (DataSourceFactory.CrearUsuarios().ExisteAlguno())
                return true;
        }
        catch (Exception ex)
        {
            Registro.Error(ex, "No se pudo conectar con la base al arrancar");
            MessageBox.Show(
                $"No se pudo conectar con la base de datos.\n\n{ex.Message}\n\n" +
                "Revisa la cadena de conexión en appsettings.local.json.",
                "ASO Adminis", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        return new PrimerArranqueView().ShowDialog() == true;
    }
}
