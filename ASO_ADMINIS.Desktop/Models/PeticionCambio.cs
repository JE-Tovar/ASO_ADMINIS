namespace ASO_ADMINIS.Desktop.Models;

/// <summary>Situacion de una peticion de cambio dentro de su flujo.</summary>
public enum EstadoPeticion
{
    Pendiente,
    Aprobada,
    Rechazada
}

/// <summary>
/// Peticion del remesero al administrador de su nucleo para una accion que el no puede
/// ejecutar (anular un documento, corregir un catalogo maestro, forzar una salida sin stock).
///
/// Es una CONSTANCIA, no una mutacion guardada: no lleva el cambio que habria que aplicar.
/// Al aprobarla, el administrador ejecuta la accion el mismo por el servicio de dominio de
/// siempre, con sus validaciones y su maquina de estados intactas. Reproducir automaticamente
/// una mutacion capturada saltaria justo esas comprobaciones, que son la parte que protege el
/// dato; y de paso el aprobador acaba siendo distinto del solicitante, que es la segregacion
/// de funciones que pide el disenno de autorizacion.
/// </summary>
public class PeticionCambio : IEntidad<int>, IDeOrganizacion
{
    public int Id { get; set; }
    public int OrganizacionId { get; set; }

    /// <summary>Permiso que le falto al solicitante, p. ej. "Remesas.Anular".</summary>
    public string Permiso { get; set; } = string.Empty;

    /// <summary>Que se queria hacer, en palabras: "Anular remesa".</summary>
    public string Accion { get; set; } = string.Empty;

    /// <summary>Tipo de la entidad afectada ("Remesa", "Tarifa"...), para navegar hasta ella.</summary>
    public string TipoEntidad { get; set; } = string.Empty;

    /// <summary>Id de la entidad afectada, como texto: hay claves int y una string (InventoryItem).</summary>
    public string EntidadId { get; set; } = string.Empty;

    /// <summary>Como se ve la entidad en pantalla, congelado al pedir: "Remesa Nº 42 · Finca La Paz".</summary>
    public string EntidadDescripcion { get; set; } = string.Empty;

    /// <summary>Por que lo pide. Obligatorio: es lo que el administrador va a juzgar.</summary>
    public string Motivo { get; set; } = string.Empty;

    public EstadoPeticion Estado { get; set; } = EstadoPeticion.Pendiente;

    public int SolicitadoPorId { get; set; }
    public string SolicitadoPorNombre { get; set; } = string.Empty;
    public DateTime SolicitadoEn { get; set; } = DateTime.Now;

    public int? ResueltoPorId { get; set; }
    public string ResueltoPorNombre { get; set; } = string.Empty;
    public DateTime? ResueltoEn { get; set; }
    public string ComentarioResolucion { get; set; } = string.Empty;

    public bool EstaPendiente => Estado == EstadoPeticion.Pendiente;

    public string EstadoTexto => Estado switch
    {
        EstadoPeticion.Pendiente => "Pendiente",
        EstadoPeticion.Aprobada => "Aprobada",
        _ => "Rechazada"
    };

    public string Resumen => $"{Accion} · {EntidadDescripcion}";

    public PeticionCambio Clonar() => (PeticionCambio)MemberwiseClone();
}
