namespace ASO_ADMINIS.Desktop.Models;

/// <summary>
/// Roles del sistema. Cada uno trae un conjunto base de permisos
/// (<c>Services/MatrizPermisos.cs</c>) que el administrador puede ajustar por usuario
/// con <see cref="PermisoUsuario"/>.
///
/// Se persisten como ORDINAL (columna <c>Usuarios.Rol int</c>, sin <c>HasConversion</c>), asi
/// que los miembros se agregan SIEMPRE al final: declarar uno en medio reinterpretaria los
/// usuarios ya guardados.
///
/// PROVISIONAL: son 4 roles genéricos, sin atar todavía a los puestos reales de la planta
/// (inspector de calidad, operario de lavado, empacador, etc.). Ajustar los nombres y el
/// conjunto base en <c>MatrizPermisos</c> en cuanto el negocio real esté definido.
/// </summary>
public enum Rol
{
    /// <summary>
    /// El día a día dentro de lo que ya existe en el sistema. Crea y edita los documentos de su
    /// área, pero no mueve dinero ni borra nada: para eso levanta una <see cref="PeticionCambio"/>.
    /// </summary>
    Operador,

    /// <summary>
    /// Supervisa el área de un Operador: aprueba lo que él inicia (pagos, movimientos de banco),
    /// resuelve las peticiones de su dominio y puede eliminar documentos de esa área. No deshace
    /// un movimiento de banco ya asentado: eso queda para quien administra toda la organización.
    /// </summary>
    Supervisor,

    /// <summary>
    /// Manda dentro de SU organización: ve y modifica todo, resuelve cualquier petición y
    /// administra los usuarios. No ve otras organizaciones y no puede crear otro Desarrollador.
    /// </summary>
    AdministradorOrganizacion,

    /// <summary>
    /// Nosotros. Puede todo, y es el único rol que reparte su propio rol.
    /// </summary>
    Desarrollador
}
