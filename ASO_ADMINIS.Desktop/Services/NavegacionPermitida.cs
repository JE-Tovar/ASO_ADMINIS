using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Navigation;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Que partes del menu ve el usuario actual. Regla unica, para que el sidebar, el lanzador de
/// Inicio, las tarjetas del dashboard y la guarda de MainWindow no puedan discrepar entre si:
/// si una tarjeta lleva a una pantalla, esa pantalla se abre.
/// </summary>
public static class NavegacionPermitida
{
    public static bool Ve(this ISesionActual sesion, Submodulo submodulo) =>
        sesion.Puede(submodulo.Permiso);

    /// <summary>
    /// Un modulo con submodulos se ve si se ve alguno de ellos: entrar a un modulo del que no
    /// se puede abrir nada seria un dashboard vacio. Los que no tienen submodulos (Inicio,
    /// Peticiones) deciden por su propio permiso.
    /// </summary>
    public static bool Ve(this ISesionActual sesion, Modulo modulo) =>
        modulo.Submodulos.Count == 0
            ? sesion.Puede(modulo.Permiso)
            : modulo.Submodulos.Any(sesion.Ve);

    public static IReadOnlyList<Modulo> ModulosVisibles(this ISesionActual sesion) =>
        [.. ModuloCatalogo.Modulos.Where(sesion.Ve)];

    public static IReadOnlyList<Submodulo> SubmodulosVisibles(this ISesionActual sesion, Modulo modulo) =>
        [.. modulo.Submodulos.Where(sesion.Ve)];
}
