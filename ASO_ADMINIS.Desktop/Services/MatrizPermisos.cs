using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Navigation;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Que puede cada rol. Es el conjunto BASE; el administrador lo ajusta por usuario con
/// <see cref="PermisoUsuario"/> y la suma la resuelve <see cref="SesionActual.IniciarSesion"/>.
///
/// El universo de permisos se arma por reflexion sobre <see cref="Permisos"/> y sobre el
/// catalogo de navegacion, a proposito: agregar un permiso o un submodulo lo mete solo en
/// "todos", de modo que un rol total no se queda corto por olvido.
///
/// PROVISIONAL: los 4 roles son genéricos (ver <see cref="Rol"/>) y los conjuntos base solo
/// cubren los dos módulos de ejemplo que existen hoy (Finanzas, Inventario). Ajustar en cuanto
/// el negocio real tenga sus propios módulos y roles.
/// </summary>
public static class MatrizPermisos
{
    /// <summary>Todos los permisos de accion declarados en <see cref="Permisos"/>.</summary>
    private static readonly HashSet<string> _acciones =
        typeof(Permisos).GetNestedTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

    /// <summary>
    /// Los permisos "Ver.*" que de verdad se consultan, derivados del catalogo de navegacion:
    /// los modulos fijados (Inicio, Peticiones, Administracion, Configuracion) y todos los
    /// submodulos.
    ///
    /// Los modulos de negocio NO entran: su visibilidad la deciden sus submodulos (ver
    /// <see cref="NavegacionPermitida"/>), asi que "Ver.Finanzas" no se lee nunca.
    /// </summary>
    private static readonly HashSet<string> _navegacion =
        ModuloCatalogo.TodosLosFijados.Select(m => m.Permiso)
            .Concat(ModuloCatalogo.Modulos.SelectMany(m => m.Submodulos).Select(s => s.Permiso))
            .ToHashSet();

    public static IReadOnlySet<string> Todos { get; } =
        _acciones.Concat(_navegacion).ToHashSet();

    /// <summary>
    /// Reservado al Desarrollador: repartir su propio rol. Un administrador manda en su
    /// organización, pero no fabrica usuarios con más alcance del que el mismo tiene.
    /// </summary>
    private static readonly HashSet<string> _soloDesarrollador =
    [
        Permisos.Usuarios.CrearDesarrollador
    ];

    /// <summary>
    /// El día a día en Finanzas: registra facturas de proveedor y proveedores nuevos, pero no
    /// mueve dinero (eso es de Supervisor) ni borra nada. Para lo demás, petición.
    /// </summary>
    private static readonly HashSet<string> _operador =
    [
        Permisos.Ver(ModuloCatalogo.Inicio.Clave),
        Permisos.Ver(ModuloCatalogo.Peticiones.Clave),
        Permisos.Ver(ModuloCatalogo.Configuracion.Clave),
        Permisos.Ver("Catalogo.Modelos"),
        Permisos.Ver("Finanzas.CuentasPorPagar"),
        Permisos.Ver("Finanzas.Banco"),
        Permisos.Ver("Finanzas.Proveedores"),
        Permisos.Ver("Inventario.Almacen"),
        Permisos.Ver("Inventario.Entradas"),
        Permisos.Ver("Inventario.Salidas"),

        Permisos.Marcas.Crear,
        Permisos.Marcas.Editar,

        Permisos.Modelos.Crear,
        Permisos.Modelos.Editar,

        Permisos.Proveedores.Crear,
        Permisos.Proveedores.Editar,

        Permisos.FacturasProveedor.Crear,
        Permisos.FacturasProveedor.Editar,

        Permisos.Articulos.Crear,
        Permisos.Articulos.Editar,

        Permisos.EntradasInventario.Crear,
        Permisos.SalidasInventario.Crear,

        Permisos.Peticiones.Solicitar

        // Fuera a propósito:
        // - Finanzas.Pagar / Banco.*: mover dinero es de Supervisor.
        // - Proveedores.Eliminar / FacturasProveedor.Eliminar / Articulos.Eliminar /
        //   Modelos.Eliminar: borrar es de Supervisor. Marcas no tiene Eliminar: no existe
        //   pantalla desde donde borrar una (ver Permisos.Marcas).
        // - EntradasInventario.Anular / SalidasInventario.Anular: deshacer un documento del
        //   almacén es de Supervisor, igual que deshacer uno de Finanzas.
    ];

    /// <summary>
    /// Supervisa Finanzas: todo lo de Operador, más registrar el pago (que además asienta el
    /// movimiento en el libro de banco) y administrar las cuentas bancarias. No deshace un
    /// movimiento ya asentado — eso es del administrador de la organización.
    /// </summary>
    private static readonly HashSet<string> _supervisor =
        _operador.Concat(
        [
            Permisos.Modelos.Eliminar,

            Permisos.Proveedores.Eliminar,
            Permisos.FacturasProveedor.Eliminar,

            Permisos.Finanzas.Pagar,
            Permisos.Finanzas.Anular,

            Permisos.Banco.Crear,
            Permisos.Banco.Editar,
            Permisos.Banco.Eliminar,
            Permisos.Banco.Conciliar,
            Permisos.Banco.Transferir,

            Permisos.CuentasBancarias.Crear,
            Permisos.CuentasBancarias.Editar,

            Permisos.Articulos.Eliminar,

            Permisos.EntradasInventario.Anular,
            Permisos.SalidasInventario.Anular,

            Permisos.Peticiones.Resolver

            // Fuera a propósito:
            // - Banco.Anular: deshacer un movimiento ya asentado queda un nivel más arriba.
        ]).ToHashSet();

    private static readonly HashSet<string> _administrador =
        Todos.Where(p => !_soloDesarrollador.Contains(p)).ToHashSet();

    /// <summary>Conjunto base del rol, antes de los ajustes por usuario.</summary>
    // Sin arco de descarte, y es deliberado: con `_ => Todos` un rol nuevo que se olvide aqui
    // se convierte en superusuario EN SILENCIO. Ahora el compilador avisa (CS8509) en cuanto se
    // declare un rol y no se mapee. Se calla CS8524, que solo se dispararia con un entero que no
    // corresponde a ningun miembro: fila corrupta, y ahi preferimos la excepcion al disimulo.
#pragma warning disable CS8524
    public static IReadOnlySet<string> Base(Rol rol) => rol switch
    {
        Rol.Operador => _operador,
        Rol.Supervisor => _supervisor,
        Rol.AdministradorOrganizacion => _administrador,
        Rol.Desarrollador => Todos
    };
#pragma warning restore CS8524

    /// <summary>
    /// Permisos que, si faltan, no dejan el boton muerto: abren una peticion al administrador.
    ///
    /// Empieza vacía: hoy no hay ninguna acción sensible gateada en las pantallas que un
    /// Operador ve. Al ampliar lo que ve un rol, sumar aquí la acción sensible correspondiente
    /// y cablear el comando con <see cref="SolicitudesDeCambio"/>, o la regla queda muerta.
    /// </summary>
    public static IReadOnlySet<string> Solicitables { get; } = new HashSet<string>();

    public static bool EsSolicitable(string permiso) => Solicitables.Contains(permiso);
}
