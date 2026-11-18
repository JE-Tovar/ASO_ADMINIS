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
    /// RETIRADO (2026-11-18): era el rol de oficina que además vendía. La migración
    /// <c>QuitarRolOperador</c> pasó a sus usuarios a <see cref="Supervisor"/>. Se conserva el
    /// miembro solo para no correr los ordinales persistidos: no se asigna y no da ningún permiso.
    /// </summary>
    OperadorRetirado,

    /// <summary>
    /// La oficina y su supervisión: compras, almacén, banco y vender (lo que antes hacía el
    /// Operador), más pagar, notas, cierre Z, eliminar, resolver peticiones de su dominio y cerrar
    /// el turno de otro cajero. No deshace un movimiento de banco ya asentado: eso queda para quien
    /// administra toda la organización.
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
    Desarrollador,

    /// <summary>
    /// Cuenta de caja: solo vende, en SU caja (<see cref="Usuario.CajaId"/>, obligatoria) y dentro
    /// de su turno. No ve Finanzas, Inventario, Reportes ni las ventas de otras cajas.
    /// Va al final por el ordinal persistido, no por jerarquía.
    /// </summary>
    Cajero
}
