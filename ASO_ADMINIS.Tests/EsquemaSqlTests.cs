using ASO_ADMINIS.Desktop.BD;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ASO_ADMINIS.Tests;

/// <summary>
/// La actualización de la base al abrir (<see cref="Esquema"/>), contra una base LocalDB propia.
/// En la colección SQL para no cargar LocalDB a la vez que las demás pruebas de integración.
/// </summary>
[Collection("SQL")]
public sealed class EsquemaSqlTests
{
    /// <summary>La última migración que traía la 0.4.0.</summary>
    private const string MigracionDeLa040 = "20261113182646_QuitarPresentacionesArticulo";

    [FactSql]
    public void Varias_cajas_que_abren_a_la_vez_no_chocan_al_migrar()
    {
        // Lo que pasó al publicar la 0.5.0: dos instancias vieron las mismas migraciones
        // pendientes y la segunda falló con "Column name 'CajaId' in table 'Ventas' is specified
        // more than once".
        var cadena = $"Server=(localdb)\\MSSQLLocalDB;Database=AsoPruebaEsquema_{Guid.NewGuid():N};" +
                     "Trusted_Connection=True;TrustServerCertificate=True;";
        using (var db = new AsoAdminisDbContext(cadena))
            db.GetService<IMigrator>().Migrate(MigracionDeLa040);

        try
        {
            int pendientes;
            using (var db = new AsoAdminisDbContext(cadena))
                pendientes = db.Database.GetPendingMigrations().Count();
            Assert.True(pendientes > 0);

            const int cajas = 4;
            using var largada = new Barrier(cajas);
            var resultados = Enumerable.Range(0, cajas)
                .Select(_ => Task.Run(() =>
                {
                    largada.SignalAndWait();
                    return Esquema.Actualizar(cadena);
                }))
                .ToArray();
            Task.WaitAll(resultados);

            // Una sola aplicó todo; las demás esperaron y no encontraron nada que hacer.
            Assert.Single(resultados, t => t.Result.Count > 0);
            Assert.Equal(pendientes, resultados.Sum(t => t.Result.Count));
            using (var db = new AsoAdminisDbContext(cadena))
                Assert.Empty(db.Database.GetPendingMigrations());
        }
        finally
        {
            using var db = new AsoAdminisDbContext(cadena);
            db.Database.EnsureDeleted();
        }
    }

    [FactSql]
    public void Con_la_base_al_dia_no_aplica_nada()
    {
        var cadena = $"Server=(localdb)\\MSSQLLocalDB;Database=AsoPruebaEsquema_{Guid.NewGuid():N};" +
                     "Trusted_Connection=True;TrustServerCertificate=True;";
        try
        {
            // Sin base: la crea entera.
            Assert.NotEmpty(Esquema.Actualizar(cadena));
            Assert.Empty(Esquema.Actualizar(cadena));
        }
        finally
        {
            using var db = new AsoAdminisDbContext(cadena);
            db.Database.EnsureDeleted();
        }
    }
}
