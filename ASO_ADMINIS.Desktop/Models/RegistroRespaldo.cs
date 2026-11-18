namespace ASO_ADMINIS.Desktop.Models;

public enum TipoRespaldo
{
    /// <summary>Copia nativa de SQL Server (.bak): restauración completa, solo en SQL Server.</summary>
    Nativo,

    /// <summary>Exportación portable (.zip con un JSON por tabla): independiente del motor.</summary>
    Portable
}

public enum OrigenRespaldo
{
    /// <summary>El primero del día, al iniciar sesión.</summary>
    Automatico,

    /// <summary>"Respaldar ahora" en Administración · Respaldos.</summary>
    Manual,

    /// <summary>El modo <c>--respaldar</c>, lanzado por el Programador de tareas.</summary>
    Programado
}

/// <summary>
/// Un respaldo hecho (o intentado). No es de una organización: el .bak es de la base entera, y la
/// exportación también lleva todas. Por eso no lleva filtro de ámbito y se ve desde cualquiera.
/// </summary>
public class RegistroRespaldo : IEntidad<int>
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public TipoRespaldo Tipo { get; set; }
    public OrigenRespaldo Origen { get; set; }

    public bool Exitoso { get; set; }

    /// <summary>El .bak pasó RESTORE VERIFYONLY (con checksum). Para la exportación: se releyó el zip.</summary>
    public bool Verificado { get; set; }

    /// <summary>Ruta del archivo. La del .bak es la del SERVIDOR de base de datos, no la de esta PC.</summary>
    public string Archivo { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public int DuracionMs { get; set; }

    public string Equipo { get; set; } = string.Empty;
    public string UsuarioNombre { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;

    // ---- Derivados para la grilla ----

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy HH:mm");
    public string TipoTexto => Tipo == TipoRespaldo.Nativo ? "SQL Server (.bak)" : "Portable (.zip)";
    public string OrigenTexto => Origen switch
    {
        OrigenRespaldo.Manual => "Manual",
        OrigenRespaldo.Programado => "Programado",
        _ => "Automático"
    };
    public string DetalleTexto => Exitoso ? Archivo : Error;
    public string EstadoTexto => !Exitoso ? "Falló" : Verificado ? "Correcto, verificado" : "Correcto";
    public string TamanoTexto => TamanoBytes switch
    {
        <= 0 => "—",
        < 1024 * 1024 => $"{TamanoBytes / 1024.0:N0} KB",
        _ => $"{TamanoBytes / (1024.0 * 1024):N1} MB"
    };
}
