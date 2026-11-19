using System.Data;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ASO_ADMINIS.Desktop.Configuration;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.BD;

/// <summary>
/// Respaldos de la base, de dos clases:
/// <list type="bullet">
///   <item><b>Nativo</b> (<c>BACKUP DATABASE</c> + <c>RESTORE VERIFYONLY</c>): la copia completa
///         y rápida, pero solo se restaura en SQL Server. El archivo lo escribe el SERVIDOR, así
///         que la carpeta es una ruta del servidor, no de esta PC.</item>
///   <item><b>Portable</b>: un zip con un JSON por tabla y un manifiesto. No depende del motor:
///         es lo que permitirá cargar los datos en PostgreSQL. Lo escribe esta PC.</item>
/// </list>
/// Las dos leen TODAS las organizaciones (<c>IgnoreQueryFilters</c>): un respaldo es de la base
/// entera, no de la organización de la sesión, y el modo programado corre sin sesión.
/// </summary>
public sealed class SqlRespaldoDataSource : IRespaldoDataSource
{
    private const int TiempoComandoSeg = 30 * 60;

    public RegistroRespaldo Anotar(RegistroRespaldo registro)
    {
        using var context = new AsoAdminisDbContext();
        context.Respaldos.Add(registro);
        context.SaveChanges();
        return registro;
    }

    public IReadOnlyList<RegistroRespaldo> Ultimos(int cuantos)
    {
        using var context = new AsoAdminisDbContext();
        return context.Respaldos.OrderByDescending(r => r.Fecha).Take(cuantos).AsNoTracking().ToList();
    }

    public DateTime? UltimoExitoso(TipoRespaldo tipo)
    {
        using var context = new AsoAdminisDbContext();
        return context.Respaldos.Where(r => r.Tipo == tipo && r.Exitoso)
            .OrderByDescending(r => r.Fecha).Select(r => (DateTime?)r.Fecha).FirstOrDefault();
    }

    // ---------------------------------------------------------------- Nativo

    public string CarpetaServidor(string? configurada, string carpetaLocal)
    {
        if (!string.IsNullOrWhiteSpace(configurada))
            return configurada.Trim();

        using var conexion = Abrir();
        // LocalDB corre con el usuario de Windows: puede escribir en la carpeta de la app.
        if (Escalar<int?>(conexion, "SELECT CAST(SERVERPROPERTY('IsLocalDB') AS int)") == 1)
            return carpetaLocal;

        return Escalar<string>(conexion, "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))")
               ?? throw new InvalidOperationException(
                   "SQL Server no informa una carpeta de respaldos por defecto. Configura Respaldos:CarpetaServidor en appsettings.local.json.");
    }

    public bool ServidorEsLocal()
    {
        using var conexion = Abrir();
        if (Escalar<int?>(conexion, "SELECT CAST(SERVERPROPERTY('IsLocalDB') AS int)") == 1)
            return true;

        var maquina = Escalar<string>(conexion, "SELECT CAST(SERVERPROPERTY('MachineName') AS nvarchar(256))");
        return string.Equals(maquina, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
    }

    public (string Ruta, long Tamano, bool Verificado) RespaldoNativo(string carpetaServidor, string nombreArchivo)
    {
        var ruta = Path.Combine(carpetaServidor, nombreArchivo);
        using var conexion = Abrir();

        if (ServidorEsLocal())
            Directory.CreateDirectory(carpetaServidor);

        // El nombre de la base se escapa a mano y no con QUOTENAME: con LocalDB y
        // AttachDbFilename el nombre es la ruta del .mdf, y QUOTENAME devuelve NULL pasados 128.
        Ejecutar(conexion, @"
DECLARE @base nvarchar(4000) = DB_NAME();
DECLARE @sql nvarchar(max) = N'BACKUP DATABASE [' + REPLACE(@base, N']', N']]') + N'] TO DISK = @ruta WITH INIT, CHECKSUM, NAME = N''ASO Adminis''';
EXEC sp_executesql @sql, N'@ruta nvarchar(4000)', @ruta = @ruta;", ruta);

        Ejecutar(conexion, "RESTORE VERIFYONLY FROM DISK = @ruta WITH CHECKSUM;", ruta);

        var tamano = File.Exists(ruta) ? new FileInfo(ruta).Length : 0;
        return (ruta, tamano, true);
    }

    // ---------------------------------------------------------------- Portable

    public (string Ruta, long Tamano) ExportacionPortable(string rutaZip, string versionApp)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(rutaZip)!);
        var temporal = rutaZip + ".parcial";

        var opciones = new JsonSerializerOptions { WriteIndented = false };
        var conteos = new Dictionary<string, int>();

        using (var context = new AsoAdminisDbContext())
        {
            context.Database.SetCommandTimeout(TiempoComandoSeg);
            var migracion = context.Database.GetAppliedMigrations().LastOrDefault() ?? string.Empty;

            using (var archivo = File.Create(temporal))
            using (var zip = new ZipArchive(archivo, ZipArchiveMode.Create))
            {
                var tipos = context.Model.GetEntityTypes().Where(t => !t.IsOwned()).OrderBy(t => t.GetTableName()).ToList();
                var exportar = typeof(SqlRespaldoDataSource).GetMethod(nameof(ExportarTabla), BindingFlags.NonPublic | BindingFlags.Static)!;

                foreach (var tipo in tipos)
                {
                    var tabla = tipo.GetTableName() ?? tipo.ClrType.Name;
                    var entrada = zip.CreateEntry($"tablas/{tabla}.json", CompressionLevel.Optimal);
                    using var flujo = entrada.Open();
                    using var escritor = new Utf8JsonWriter(flujo);
                    conteos[tabla] = (int)exportar.MakeGenericMethod(tipo.ClrType).Invoke(null, [context, tipo, escritor, opciones])!;
                }

                var manifiesto = zip.CreateEntry("manifiesto.json");
                using (var flujo = manifiesto.Open())
                    JsonSerializer.Serialize(flujo, new
                    {
                        Formato = "aso-adminis-exportacion",
                        VersionFormato = 1,
                        VersionApp = versionApp,
                        Fecha = DateTime.Now,
                        UltimaMigracion = migracion,
                        Tablas = conteos
                    }, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        // Se relee entero antes de darlo por bueno: un zip cortado (disco lleno) no debe
        // reemplazar a nadie ni anotarse como respaldo.
        using (var comprobar = ZipFile.OpenRead(temporal))
        {
            if (comprobar.GetEntry("manifiesto.json") is null)
                throw new InvalidOperationException("La exportación quedó incompleta (sin manifiesto).");
            foreach (var e in comprobar.Entries)
            {
                using var s = e.Open();
                s.CopyTo(Stream.Null);
            }
        }

        File.Move(temporal, rutaZip, overwrite: true);
        return (rutaZip, new FileInfo(rutaZip).Length);
    }

    /// <summary>
    /// Una tabla como arreglo JSON: por fila, sus columnas (por nombre de propiedad) y sus
    /// colecciones owned anidadas (renglones, pagos). Las claves ocultas de los owned se omiten:
    /// la anidación ya dice de quién son.
    /// </summary>
    private static int ExportarTabla<T>(AsoAdminisDbContext context, IEntityType tipo, Utf8JsonWriter escritor,
                                        JsonSerializerOptions opciones) where T : class
    {
        var filas = 0;
        escritor.WriteStartArray();
        foreach (var fila in context.Set<T>().IgnoreQueryFilters().AsNoTracking().AsEnumerable())
        {
            EscribirObjeto(fila, tipo, escritor, opciones);
            filas++;
        }
        escritor.WriteEndArray();
        escritor.Flush();
        return filas;
    }

    private static void EscribirObjeto(object objeto, IEntityType tipo, Utf8JsonWriter escritor, JsonSerializerOptions opciones)
    {
        escritor.WriteStartObject();

        foreach (var propiedad in tipo.GetProperties().Where(p => p.PropertyInfo is not null))
        {
            escritor.WritePropertyName(propiedad.Name);
            var valor = propiedad.PropertyInfo!.GetValue(objeto);
            JsonSerializer.Serialize(escritor, valor, propiedad.ClrType, opciones);
        }

        foreach (var navegacion in tipo.GetNavigations().Where(n => n.TargetEntityType.IsOwned() && n.PropertyInfo is not null))
        {
            escritor.WritePropertyName(navegacion.Name);
            var valor = navegacion.PropertyInfo!.GetValue(objeto);
            if (valor is System.Collections.IEnumerable lista && navegacion.IsCollection)
            {
                escritor.WriteStartArray();
                foreach (var hijo in lista)
                    EscribirObjeto(hijo, navegacion.TargetEntityType, escritor, opciones);
                escritor.WriteEndArray();
            }
            else if (valor is not null)
            {
                EscribirObjeto(valor, navegacion.TargetEntityType, escritor, opciones);
            }
            else
            {
                escritor.WriteNullValue();
            }
        }

        escritor.WriteEndObject();
    }

    // ---------------------------------------------------------------- Candado

    public IDisposable? TomarCandadoDeRespaldo()
    {
        var conexion = Abrir();
        var resultado = new SqlParameter("@r", SqlDbType.Int) { Direction = ParameterDirection.Output };
        using (var comando = new SqlCommand(
                   "EXEC @r = sp_getapplock @Resource = N'ASO:Respaldo', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;",
                   conexion))
        {
            comando.Parameters.Add(resultado);
            comando.ExecuteNonQuery();
        }

        if (resultado.Value is int r && r >= 0)
            return new CandadoDeSesion(conexion, "ASO:Respaldo");

        conexion.Dispose();
        return null;
    }

    /// <summary>
    /// Suelta el candado y después cierra. Cerrar a secas no basta: la conexión vuelve al pool con
    /// la sesión viva y el candado puesto, y otra caja que respalde en esos minutos lo vería
    /// tomado y se saltaría su respaldo.
    /// </summary>
    private sealed class CandadoDeSesion(SqlConnection conexion, string recurso) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                using var comando = new SqlCommand(
                    "EXEC sp_releaseapplock @Resource = @recurso, @LockOwner = 'Session';", conexion);
                comando.Parameters.AddWithValue("@recurso", recurso);
                comando.ExecuteNonQuery();
            }
            catch (SqlException)
            {
                // Conexión rota: SqlClient la descarta, la sesión muere y el candado se suelta solo.
            }
            finally
            {
                conexion.Dispose();
            }
        }
    }

    // ---------------------------------------------------------------- Apoyo

    private static SqlConnection Abrir()
    {
        var conexion = new SqlConnection(AppConfig.ConnectionString);
        conexion.Open();
        return conexion;
    }

    private static T? Escalar<T>(SqlConnection conexion, string sql)
    {
        using var comando = new SqlCommand(sql, conexion);
        var valor = comando.ExecuteScalar();
        return valor is null or DBNull ? default : (T)Convert.ChangeType(valor, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    private static void Ejecutar(SqlConnection conexion, string sql, string ruta)
    {
        using var comando = new SqlCommand(sql, conexion) { CommandTimeout = TiempoComandoSeg };
        comando.Parameters.Add(new SqlParameter("@ruta", SqlDbType.NVarChar, 4000) { Value = ruta });
        comando.ExecuteNonQuery();
    }
}
