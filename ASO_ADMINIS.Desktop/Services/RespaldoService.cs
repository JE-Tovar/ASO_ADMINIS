using System.Diagnostics;
using System.IO;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>Cómo terminó un respaldo completo (los dos tipos).</summary>
public sealed record ResultadoRespaldo(bool Hecho, IReadOnlyList<RegistroRespaldo> Registros, string Resumen)
{
    public bool TodoBien => Hecho && Registros.All(r => r.Exitoso);
}

/// <summary>
/// Respaldos: el nativo de SQL Server (.bak, verificado) y la exportación portable (.zip), uno
/// detrás del otro, con su historial y la limpieza de los viejos.
///
/// <para><b>Cuándo:</b> solo una vez al día, en el primer inicio de sesión de cualquier PC
/// (<see cref="RespaldarSiToca"/>); a mano con "Respaldar ahora"; o con el modo
/// <c>--respaldar</c> desde el Programador de tareas del servidor. SQL Server Express no trae el
/// Agente que haría esto solo.</para>
///
/// <para><b>Uno a la vez:</b> si dos PCs entran juntas, la segunda ve el candado tomado y no
/// respalda (<see cref="IRespaldoDataSource.TomarCandadoDeRespaldo"/>).</para>
///
/// <para><b>Retención:</b> quedan los <see cref="Conservar"/> más recientes de cada tipo. Los .bak
/// solo se limpian si la base está en esta misma PC: la carpeta del servidor no es de aquí.</para>
/// </summary>
public sealed class RespaldoService(IRespaldoDataSource fuente, OpcionesRespaldo opciones)
{
    public const int Conservar = 14;

    /// <summary>Con un exitoso más nuevo que esto, el automático no se repite.</summary>
    public static readonly TimeSpan Frecuencia = TimeSpan.FromHours(20);

    public string CarpetaLocal => opciones.CarpetaLocal;

    public IReadOnlyList<RegistroRespaldo> Historial(int cuantos = 50) => fuente.Ultimos(cuantos);

    /// <summary>¿Hace falta el automático? Falta si alguno de los dos tipos no tiene uno exitoso reciente.</summary>
    public bool Toca(DateTime ahora) =>
        Enum.GetValues<TipoRespaldo>().Any(t => fuente.UltimoExitoso(t) is not { } f || ahora - f >= Frecuencia);

    /// <summary>El automático del inicio de sesión: solo si toca y si ninguna otra PC lo está haciendo.</summary>
    public ResultadoRespaldo RespaldarSiToca(string usuario)
    {
        if (!Toca(DateTime.Now))
            return new ResultadoRespaldo(false, [], "Ya hay respaldos recientes.");

        return Respaldar(OrigenRespaldo.Automatico, usuario);
    }

    public ResultadoRespaldo Respaldar(OrigenRespaldo origen, string usuario)
    {
        using var candado = fuente.TomarCandadoDeRespaldo();
        if (candado is null)
            return new ResultadoRespaldo(false, [], "Otra PC está haciendo un respaldo en este momento.");

        // Con el candado tomado se vuelve a mirar: la otra PC pudo terminar uno justo antes.
        if (origen == OrigenRespaldo.Automatico && !Toca(DateTime.Now))
            return new ResultadoRespaldo(false, [], "Otra PC acaba de respaldar.");

        var marca = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var registros = new List<RegistroRespaldo>
        {
            Intentar(TipoRespaldo.Nativo, origen, usuario, () =>
            {
                var carpeta = fuente.CarpetaServidor(opciones.CarpetaServidor, opciones.CarpetaLocal);
                var (ruta, tamano, verificado) = fuente.RespaldoNativo(carpeta, $"aso-respaldo-{marca}.bak");
                return (ruta, tamano, verificado);
            }),
            Intentar(TipoRespaldo.Portable, origen, usuario, () =>
            {
                var (ruta, tamano) = fuente.ExportacionPortable(
                    Path.Combine(opciones.CarpetaLocal, $"aso-exportacion-{marca}.zip"), Registro.Version);
                return (ruta, tamano, true);
            })
        };

        Limpiar();

        var resumen = string.Join("\n", registros.Select(r => r.Exitoso
            ? $"{r.TipoTexto}: {r.EstadoTexto} · {r.TamanoTexto} · {r.Archivo}"
            : $"{r.TipoTexto}: FALLÓ · {r.Error}"));
        return new ResultadoRespaldo(true, registros, resumen);
    }

    private RegistroRespaldo Intentar(TipoRespaldo tipo, OrigenRespaldo origen, string usuario,
                                      Func<(string Ruta, long Tamano, bool Verificado)> hacer)
    {
        var reloj = Stopwatch.StartNew();
        var registro = new RegistroRespaldo
        {
            Fecha = DateTime.Now,
            Tipo = tipo,
            Origen = origen,
            Equipo = Environment.MachineName,
            UsuarioNombre = usuario
        };

        try
        {
            var (ruta, tamano, verificado) = hacer();
            registro.Exitoso = true;
            registro.Verificado = verificado;
            registro.Archivo = ruta;
            registro.TamanoBytes = tamano;
            Registro.Info("Respaldo {Tipo} ({Origen}) en {Ruta}, {Bytes} bytes", tipo, origen, ruta, tamano);
        }
        catch (Exception ex)
        {
            registro.Exitoso = false;
            registro.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            Registro.Error(ex, "Falló el respaldo {Tipo} ({Origen})", tipo, origen);
        }

        registro.DuracionMs = (int)reloj.ElapsedMilliseconds;

        try
        {
            return fuente.Anotar(registro);
        }
        catch (Exception ex)
        {
            // Sin historial el respaldo sigue valiendo: el archivo está. Que quede al menos en el log.
            Registro.Error(ex, "No se pudo anotar el respaldo en el historial");
            return registro;
        }
    }

    /// <summary>Deja los <see cref="Conservar"/> más nuevos de cada patrón en las carpetas que son de esta PC.</summary>
    private void Limpiar()
    {
        try
        {
            LimpiarPatron(opciones.CarpetaLocal, "aso-exportacion-*.zip");

            var servidorLocal = fuente.ServidorEsLocal();
            var carpetaBak = fuente.CarpetaServidor(opciones.CarpetaServidor, opciones.CarpetaLocal);
            if (servidorLocal)
                LimpiarPatron(carpetaBak, "aso-respaldo-*.bak");
        }
        catch (Exception ex)
        {
            Registro.Advertencia(ex, "No se pudieron limpiar los respaldos viejos");
        }
    }

    private static void LimpiarPatron(string carpeta, string patron)
    {
        if (!Directory.Exists(carpeta))
            return;

        // Solo archivos con nuestro nombre: nunca se toca otra cosa que haya en la carpeta.
        foreach (var viejo in new DirectoryInfo(carpeta).GetFiles(patron)
                     .OrderByDescending(f => f.Name, StringComparer.Ordinal)
                     .Skip(Conservar))
        {
            viejo.Delete();
            Registro.Info("Respaldo viejo eliminado: {Archivo}", viejo.FullName);
        }
    }
}

/// <summary>
/// Dónde van los respaldos. <see cref="CarpetaLocal"/> es de esta PC (la exportación, y el .bak
/// con LocalDB); <see cref="CarpetaServidor"/> es una ruta DEL SERVIDOR de SQL para el .bak, y si
/// es null se usa la de SQL Server por defecto.
/// </summary>
public sealed record OpcionesRespaldo(string CarpetaLocal, string? CarpetaServidor)
{
    public static string CarpetaLocalPorDefecto => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ASO Adminis", "Respaldos");

    /// <summary>De appsettings: <c>Respaldos:Carpeta</c> y <c>Respaldos:CarpetaServidor</c>.</summary>
    public static OpcionesRespaldo DeConfiguracion() => new(
        Configuration.AppConfig.Valor("Respaldos:Carpeta") is { Length: > 0 } local ? local : CarpetaLocalPorDefecto,
        Configuration.AppConfig.Valor("Respaldos:CarpetaServidor"));
}
