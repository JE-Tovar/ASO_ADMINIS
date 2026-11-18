namespace ASO_ADMINIS.Desktop.Models;

public enum AccionAuditoria
{
    Alta,
    Modificacion,
    Baja
}

/// <summary>Un campo que cambió: cómo estaba y cómo quedó, ya como texto.</summary>
public sealed record CambioDeCampo(string Campo, string Antes, string Despues);

/// <summary>
/// Una fila de la bitácora: quién cambió qué, cuándo y desde dónde. La escribe sola
/// <c>AsoAdminisDbContext.SaveChanges</c> por cada alta, modificación o baja, así que ninguna
/// pantalla ni servicio tiene que acordarse de registrar.
///
/// <b>Solo se agrega.</b> El contexto rechaza modificarla o borrarla, y la base tiene además un
/// trigger que lo impide aunque se intente por fuera de la aplicación.
/// </summary>
public class RegistroAuditoria : IEntidad<int>, IDeOrganizacion
{
    public int Id { get; set; }
    public int OrganizacionId { get; set; }

    public DateTime Fecha { get; set; }

    /// <summary>Nulo si no había sesión (primer arranque).</summary>
    public int? UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public string CajaCodigo { get; set; } = string.Empty;
    public string Equipo { get; set; } = string.Empty;

    /// <summary>Tipo de registro afectado ("Articulo", "Usuario"…).</summary>
    public string Entidad { get; set; } = string.Empty;
    public string EntidadId { get; set; } = string.Empty;

    public AccionAuditoria Accion { get; set; }

    /// <summary>Una línea legible: "Modificación de Articulo #12: PrecioVenta, Activo".</summary>
    public string Resumen { get; set; } = string.Empty;

    /// <summary>JSON con la lista de <see cref="CambioDeCampo"/>.</summary>
    public string Detalle { get; set; } = string.Empty;

    // ---- Derivados para la grilla ----

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy HH:mm:ss");

    public string AccionTexto => Accion switch
    {
        AccionAuditoria.Alta => "Alta",
        AccionAuditoria.Baja => "Baja",
        _ => "Modificación"
    };

    public string EntidadTexto => Services.Auditoria.NombreLegible(Entidad);
}
