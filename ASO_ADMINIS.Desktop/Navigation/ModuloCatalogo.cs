using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Controls;

namespace ASO_ADMINIS.Desktop.Navigation;

public sealed record Submodulo(string Clave, string Nombre, string Descripcion, string Icono)
{
    /// <summary>
    /// Permiso de navegación, DERIVADO de la clave en vez de declarado aparte: así no puede
    /// desincronizarse al renombrar un submódulo. Ej.: "Ver.Finanzas.CuentasPorPagar".
    /// </summary>
    public string Permiso => Services.Permisos.Ver(Clave);
}

public sealed record Modulo(
    string Clave,
    string Nombre,
    string Descripcion,
    string Icono,
    IReadOnlyList<Submodulo> Submodulos)
{
    /// <summary>Permiso propio. Solo decide por sí mismo en los módulos SIN submódulos
    /// (Inicio, Peticiones); en el resto la visibilidad la deciden sus submódulos.</summary>
    public string Permiso => Services.Permisos.Ver(Clave);
}

/// <summary>
/// Fuente única de la estructura de navegación. El sidebar, el lanzador de inicio, los
/// dashboards y el enrutado de MainWindow leen de aquí, así que agregar o renombrar un
/// submódulo se hace en un solo lugar.
///
/// Catálogo es el primer módulo real del negocio (zapatería): Marcas y Modelos, éste último
/// conectado con Inventario (ver <see cref="Services.CatalogoService"/>). Finanzas e Inventario
/// siguen siendo los módulos de ejemplo del scaffold, como plantilla viva de los patrones del
/// framework (ver "Cómo se agrega un submódulo" en CLAUDE.md).
/// </summary>
public static class ModuloCatalogo
{
    public static Modulo Inicio { get; } = new(
        "Inicio",
        "Inicio",
        "Punto de entrada a los módulos del sistema.",
        Iconos.Inicio,
        []);

    /// <summary>
    /// Bandeja de peticiones de cambio. Es un pseudo-módulo fijado en el menú, igual que
    /// <see cref="Inicio"/>: no cuelga de ningún módulo de negocio porque las peticiones
    /// atraviesan todos.
    /// </summary>
    public static Modulo Peticiones { get; } = new(
        "Peticiones",
        "Peticiones",
        "Solicitudes de cambio pendientes de aprobación.",
        Iconos.Peticiones,
        []);

    public static IReadOnlyList<Modulo> Modulos { get; } =
    [
        new Modulo(
            "Catalogo",
            "Catálogo",
            "Modelos que vende la organización.",
            Iconos.Catalogo,
            [
                new Submodulo("Catalogo.Modelos", "Modelos",
                    "Modelos que vende la organización, con su existencia en Inventario.", Iconos.Modelo)
            ]),

        new Modulo(
            "Ventas",
            "Ventas",
            "Registro de ventas a clientes.",
            Iconos.Ventas,
            [
                new Submodulo("Ventas.Registro", "Registro de ventas",
                    "Ventas registradas, con lo que descuentan del almacén.", Iconos.Ventas)
            ]),

        new Modulo(
            "Finanzas",
            "Finanzas",
            "Pagos a proveedores y libro de banco.",
            Iconos.Finanzas,
            [
                new Submodulo("Finanzas.CuentasPorPagar", "Cuentas por Pagar",
                    "Obligaciones con proveedores y su vencimiento.", Iconos.CuentasPorPagar),
                new Submodulo("Finanzas.Banco", "Banco",
                    "Estado de la cuenta según lo cobrado y pagado en la aplicación.", Iconos.Banco),
                new Submodulo("Finanzas.Proveedores", "Proveedores",
                    "Maestro de proveedores de la organización.", Iconos.Proveedor)
            ]),

        new Modulo(
            "Inventario",
            "Inventario",
            "Existencias del almacén, entradas y salidas.",
            Iconos.Inventario,
            [
                new Submodulo("Inventario.Almacen", "Almacén",
                    "Existencias de todo lo que la organización guarda en el almacén.", Iconos.Almacen),
                new Submodulo("Inventario.Entradas", "Entradas",
                    "Historial de entradas y registro de lo que llega al almacén.", Iconos.Entradas),
                new Submodulo("Inventario.Salidas", "Salidas",
                    "Historial de salidas y emisión de boletos de salida.", Iconos.Salidas)
            ]),

        new Modulo(
            "Reportes",
            "Reportes",
            "Ventas y gastos de la organización.",
            Iconos.Reportes,
            [
                new Submodulo("Reportes.Ventas", "Ventas",
                    "Ventas registradas en el período, con totales y gráficos.", Iconos.Ventas),
                new Submodulo("Reportes.Gastos", "Gastos",
                    "Pagos a proveedores en el período, con totales y gráficos.", Iconos.Reportes)
            ]),
    ];

    /// <summary>
    /// Administración del sistema: usuarios, permisos y los datos de la propia organización.
    /// Fijado como <see cref="Peticiones"/>, y por el mismo motivo: no pertenece a ningún
    /// módulo de negocio.
    /// </summary>
    public static Modulo Administracion { get; } = new(
        "Administracion",
        "Administración",
        "Usuarios, roles, permisos y los datos de la organización.",
        Iconos.Administracion,
        []);

    /// <summary>
    /// Preferencias de quien usa la aplicación en esta máquina: apariencia, la propia cuenta y
    /// los ajustes de la app. Es un módulo fijado más, pero NO entra en <see cref="Fijados"/>
    /// porque esa lista es el orden del menú de arriba y este va anclado al pie del sidebar,
    /// donde se busca lo que no forma parte del trabajo del día.
    /// </summary>
    public static Modulo Configuracion { get; } = new(
        "Configuracion",
        "Configuración",
        "Apariencia, tu cuenta y las preferencias de la aplicación.",
        Iconos.Configuracion,
        []);

    /// <summary>Los módulos fijados que se listan arriba, en orden de menú.</summary>
    public static IReadOnlyList<Modulo> Fijados { get; } = [Inicio, Peticiones, Administracion];

    /// <summary>
    /// Todo lo fijado, incluida <see cref="Configuracion"/>, que se pinta aparte. Es la lista
    /// que hay que mirar para preguntas de permisos y de resolución de claves: si se usara
    /// <see cref="Fijados"/>, "Ver.Configuracion" no existiría en la matriz y no habría forma
    /// de quitarle la sección a nadie.
    /// </summary>
    public static IReadOnlyList<Modulo> TodosLosFijados { get; } = [.. Fijados, Configuracion];

    public static Modulo? BuscarModulo(string clave)
        => TodosLosFijados.FirstOrDefault(m => m.Clave == clave)
           ?? Modulos.FirstOrDefault(m => m.Clave == clave);

    /// <summary>Resuelve un submódulo por su clave completa ("Modulo.Submodulo").</summary>
    public static Submodulo? BuscarSubmodulo(string clave)
        => Modulos.SelectMany(m => m.Submodulos).FirstOrDefault(s => s.Clave == clave);
}
