using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Organizacion sobre la que trabaja la sesion actual: la empresa donde esta instalado el
/// sistema.
///
/// Es estatico y global a proposito: <see cref="BD.AsoAdminisDbContext"/> se construye sin argumentos
/// en cada metodo de las fuentes Sql, asi que necesita una fuente ambiental de la que leer el
/// ambito. Lo fija <see cref="SesionActual.IniciarSesion"/> al autenticar, a partir de la
/// pertenencia del usuario, y no cambia mientras dure la sesion: una instalacion atiende a una
/// sola organizacion.
///
/// Fail-closed: si no hay organizacion fijada no se ve NADA, en vez de verse todo. Un ambito sin
/// fijar es un error de programacion, no un permiso implicito.
/// </summary>
public static class Ambito
{
    /// <summary>Organizacion activa; null mientras no haya sesion iniciada.</summary>
    public static Organizacion? Actual { get; private set; }

    public static int? OrganizacionId => Actual?.Id;

    public static bool EstaFijado => Actual is not null;

    internal static void Fijar(Organizacion? organizacion) => Actual = organizacion;

    /// <summary>
    /// Refresca la copia cacheada despues de editar los datos de la organizacion, para que lo
    /// que se muestre a continuacion refleje el nombre/codigo nuevo y no el de antes.
    /// </summary>
    internal static void Actualizar(Organizacion organizacion)
    {
        if (Actual is null || Actual.Id != organizacion.Id)
            throw new InvalidOperationException(
                "Solo se refrescan los datos de la organizacion activa de la sesion.");

        Actual = organizacion;
    }

    /// <summary>Organizacion activa, o excepcion si no hay ninguna. Para escrituras.</summary>
    public static int Exigir() =>
        Actual?.Id ?? throw new InvalidOperationException(
            "No hay una organizacion activa en la sesion. Inicie sesion antes de operar sobre datos.");
}
