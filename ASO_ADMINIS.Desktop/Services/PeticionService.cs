using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Reglas del flujo de peticiones de cambio. Como todo servicio de dominio del proyecto,
/// expone predicados <c>PuedeX</c> puros para el <c>CanExecute</c> y transiciones que
/// revalidan y lanzan en espannol si se las llama fuera de sitio.
/// </summary>
public class PeticionService
{
    private readonly IPeticionCambioDataSource _fuente;
    private readonly ISesionActual _sesion;

    /// <summary>
    /// La sesion es OBLIGATORIA, no un parametro opcional con default null: de ella sale la
    /// unica regla que acota quien puede resolver que, y un hueco ahi dejaria pasar en silencio
    /// justo lo que esta clase existe para impedir.
    /// </summary>
    public PeticionService(IPeticionCambioDataSource fuente, ISesionActual sesion)
    {
        _fuente = fuente;
        _sesion = sesion;
    }

    /// <summary>Una peticion solo se resuelve una vez: aprobada o rechazada es terminal.</summary>
    public bool PuedeResolver(PeticionCambio peticion) => peticion.EstaPendiente;

    /// <summary>
    /// Solo resuelve una peticion quien tenga el permiso que se esta pidiendo.
    ///
    /// Sale de que aprobar AUTORIZA, NO EJECUTA: despues de aprobar, el cambio lo hace a mano
    /// quien aprobo, en la pantalla que corresponde. Un aprobador sin ese permiso dejaria la
    /// peticion aprobada y a nadie capaz de cumplirla, y de paso votaria sobre un modulo que no
    /// es suyo — un almacenista aprobando la anulacion de una remesa.
    ///
    /// Para AdministradorNucleo y Desarrollador no cambia nada: los tienen todos.
    /// </summary>
    public bool EsDeSuDominio(PeticionCambio peticion) => _sesion.Puede(peticion.Permiso);

    public PeticionCambio Crear(string permiso,
                                string accion,
                                string tipoEntidad,
                                string entidadId,
                                string entidadDescripcion,
                                string motivo,
                                Usuario solicitante)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("La petición necesita un motivo.");

        return _fuente.Add(new PeticionCambio
        {
            Permiso = permiso,
            Accion = accion,
            TipoEntidad = tipoEntidad,
            EntidadId = entidadId,
            EntidadDescripcion = entidadDescripcion,
            Motivo = motivo.Trim(),
            Estado = EstadoPeticion.Pendiente,
            SolicitadoPorId = solicitante.Id,
            SolicitadoPorNombre = solicitante.NombreCompleto,
            SolicitadoEn = DateTime.Now
        });
    }

    /// <summary>
    /// Marca la peticion como aprobada. NO ejecuta el cambio: la accion la hace el
    /// administrador por la pantalla correspondiente, con las validaciones y la maquina de
    /// estados de ese documento. Reproducir aqui una mutacion guardada saltaria justo esas
    /// comprobaciones, que son lo que protege el dato.
    /// </summary>
    public PeticionCambio Aprobar(PeticionCambio peticion, Usuario aprobador, string comentario) =>
        Resolver(peticion, aprobador, comentario, EstadoPeticion.Aprobada);

    public PeticionCambio Rechazar(PeticionCambio peticion, Usuario aprobador, string comentario) =>
        Resolver(peticion, aprobador, comentario, EstadoPeticion.Rechazada);

    private PeticionCambio Resolver(PeticionCambio peticion,
                                    Usuario aprobador,
                                    string comentario,
                                    EstadoPeticion destino)
    {
        if (!PuedeResolver(peticion))
            throw new InvalidOperationException(
                $"La petición ya está {peticion.EstadoTexto.ToLowerInvariant()}; no se puede volver a resolver.");

        if (!EsDeSuDominio(peticion))
            throw new InvalidOperationException(
                $"Esta petición pide «{peticion.Permiso}», que no está entre tus permisos. " +
                "La resuelve quien pueda ejecutar el cambio después de aprobarlo.");

        // Segregacion de funciones: quien pide no aprueba lo suyo.
        if (peticion.SolicitadoPorId == aprobador.Id)
            throw new InvalidOperationException("No se puede resolver una petición propia.");

        var copia = peticion.Clonar();
        copia.Estado = destino;
        copia.ResueltoPorId = aprobador.Id;
        copia.ResueltoPorNombre = aprobador.NombreCompleto;
        copia.ResueltoEn = DateTime.Now;
        copia.ComentarioResolucion = comentario?.Trim() ?? string.Empty;

        _fuente.Update(copia);
        return copia;
    }
}
