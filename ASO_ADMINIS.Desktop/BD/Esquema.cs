using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace ASO_ADMINIS.Desktop.BD;

/// <summary>
/// Pone la base al día con las migraciones del código, de a una caja por vez.
///
/// <para><b>Por qué el candado.</b> EF Core 8 no serializa <c>Migrate</c> (eso llegó en EF Core 9):
/// si dos instancias ven la misma migración pendiente, las dos la aplican, y la segunda choca con
/// lo que ya hizo la primera ("Column name 'CajaId' in table 'Ventas' is specified more than
/// once"). Pasa al abrir la app dos veces seguidas tras una actualización, o con varias cajas
/// abriendo a la vez contra la misma base. El candado es <c>sp_getapplock</c> de sesión: quien
/// llega segundo espera, vuelve a preguntar qué falta y normalmente ya no queda nada.</para>
///
/// <para>Sin base todavía (primer arranque en una máquina) no hay dónde tomar el candado: se crea
/// sin él, como antes. Que dos instancias estrenen la misma base a la vez no es un caso real.</para>
/// </summary>
public static class Esquema
{
    private const string Recurso = "ASO:Migraciones";

    /// <summary>Cuánto espera la segunda caja a que la primera termine de migrar.</summary>
    internal static TimeSpan Espera { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Aplica lo pendiente y devuelve qué aplicó esta llamada (vacío si ya estaba al día o si otra
    /// instancia lo hizo mientras esperaba).
    /// </summary>
    public static IReadOnlyList<string> Actualizar(string? cadenaConexion = null)
    {
        using var db = cadenaConexion is null ? new AsoAdminisDbContext() : new AsoAdminisDbContext(cadenaConexion);

        if (!db.GetService<IRelationalDatabaseCreator>().Exists())
        {
            var todas = db.Database.GetMigrations().ToList();
            db.Database.Migrate();
            return todas;
        }

        // El caso normal, base ya al día: ni candado ni espera.
        if (!db.Database.GetPendingMigrations().Any())
            return [];

        // Abierta a mano para que el candado (dueño: la sesión) dure hasta el final: EF no cierra
        // una conexión que no abrió él.
        db.Database.OpenConnection();
        var tomado = false;
        try
        {
            TomarCandado(db);
            tomado = true;

            var pendientes = db.Database.GetPendingMigrations().ToList();
            if (pendientes.Count > 0)
                db.Database.Migrate();
            return pendientes;
        }
        finally
        {
            if (tomado)
                SoltarCandado(db);
            db.Database.CloseConnection();
        }
    }

    /// <summary>
    /// Hay que soltarlo a mano: cerrar la conexión NO lo suelta, porque vuelve al pool con la
    /// sesión viva, y las demás cajas esperarían hasta que el pool la descarte (minutos).
    /// </summary>
    private static void SoltarCandado(AsoAdminisDbContext db)
    {
        try
        {
            db.Database.ExecuteSqlRaw(
                $"EXEC sp_releaseapplock @Resource = N'{Recurso}', @LockOwner = 'Session';");
        }
        catch (SqlException)
        {
            // Conexión rota: SqlClient la descarta del pool, la sesión muere y el servidor suelta
            // el candado solo. Tapar la migración ya hecha con este error sería peor.
        }
    }

    private static void TomarCandado(AsoAdminisDbContext db)
    {
        var resultado = new SqlParameter("@r", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var espera = new SqlParameter("@espera", SqlDbType.Int) { Value = (int)Espera.TotalMilliseconds };

        // El comando no puede vencer antes que la espera del candado.
        var anterior = db.Database.GetCommandTimeout();
        db.Database.SetCommandTimeout(Espera + TimeSpan.FromSeconds(30));
        try
        {
            db.Database.ExecuteSqlRaw(
                $"EXEC @r = sp_getapplock @Resource = N'{Recurso}', @LockMode = 'Exclusive', " +
                "@LockOwner = 'Session', @LockTimeout = @espera;",
                resultado, espera);
        }
        finally
        {
            db.Database.SetCommandTimeout(anterior);
        }

        if (resultado.Value is not int r || r < 0)
            throw new InvalidOperationException(
                "Otra caja está actualizando la base de datos y todavía no termina. " +
                "Espera unos minutos y vuelve a abrir ASO Adminis.");
    }
}
