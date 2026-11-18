using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Turnos de caja: el cajero abre con un fondo en efectivo, factura dentro del turno y al irse lo
/// cierra contando el efectivo (arqueo).
///
/// <b>Reglas:</b>
/// <list type="bullet">
///   <item>Se abre en la caja de la sesión (<see cref="ISesionActual.CajaActual"/>), nunca en otra.</item>
///   <item>Una caja tiene a lo sumo un turno abierto. Respaldado por un índice único filtrado en
///         la base, por si dos PCs abren a la vez.</item>
///   <item>Para facturar hace falta un turno abierto propio (<see cref="TurnoParaFacturar"/>,
///         que usa <see cref="VentasService"/>).</item>
///   <item>Cerrar el turno de otro exige <see cref="Permisos.Turnos.CerrarAjeno"/>.</item>
/// </list>
///
/// Lo esperado es: el fondo + el efectivo cobrado en las facturas vigentes del turno − el vuelto
/// (que se da en bolívares). Las notas de crédito no llevan pagos en el modelo, así que no
/// restan: un reintegro en efectivo se anota en las observaciones del cierre.
/// </summary>
public sealed class TurnosService(ITurnoDataSource turnos, IVentaDataSource ventas, ISesionActual sesion)
{
    /// <summary>El turno abierto de la caja de la sesión, sea de quien sea.</summary>
    public Turno? TurnoAbierto() =>
        sesion.CajaActual is { } caja ? turnos.AbiertoDe(caja.Id) : null;

    /// <summary>
    /// El turno en que puede facturar el usuario de la sesión: abierto, en su caja y suyo. Nulo si
    /// no hay (otro cajero tiene la caja, o nadie la abrió).
    /// </summary>
    public Turno? TurnoParaFacturar() =>
        TurnoAbierto() is { } t && t.UsuarioId == sesion.UsuarioActual?.Id ? t : null;

    /// <summary>Turnos para supervisar: todos con <see cref="Permisos.Ventas.VerTodasLasCajas"/>, si no los de la caja propia.</summary>
    public IReadOnlyList<Turno> Historial()
    {
        var todos = turnos.GetAll();
        if (!sesion.Puede(Permisos.Ventas.VerTodasLasCajas))
        {
            var cajaId = sesion.CajaActual?.Id;
            todos = todos.Where(t => t.CajaId == cajaId);
        }

        return [.. todos.OrderByDescending(t => t.Apertura)];
    }

    public Turno Abrir(decimal fondoBs, decimal fondoDivisa)
    {
        if (!sesion.Puede(Permisos.Turnos.Abrir))
            throw new InvalidOperationException("No tienes permiso para abrir turnos de caja.");

        var caja = sesion.CajaActual
            ?? throw new InvalidOperationException("Tu usuario no tiene caja asignada. Pídela al administrador en Administración · Usuarios.");
        var usuario = sesion.UsuarioActual
            ?? throw new InvalidOperationException("No hay sesión iniciada.");

        if (fondoBs < 0 || fondoDivisa < 0)
            throw new InvalidOperationException("El fondo no puede ser negativo.");

        if (turnos.AbiertoDe(caja.Id) is { } abierto)
            throw new InvalidOperationException(YaAbierto(caja, abierto));

        var nuevo = new Turno
        {
            CajaId = caja.Id,
            CajaCodigo = caja.Codigo,
            UsuarioId = usuario.Id,
            UsuarioNombre = usuario.NombreCompleto,
            Estado = EstadoTurno.Abierto,
            Apertura = DateTime.Now,
            FondoBs = fondoBs,
            FondoDivisa = fondoDivisa
        };

        try
        {
            return turnos.Add(nuevo);
        }
        catch (Exception) when (turnos.AbiertoDe(caja.Id) is { } rival)
        {
            // Otra PC abrió la misma caja entre la comprobación y el guardado: el índice único
            // rechazó el segundo. Se informa quién la tiene en vez del error crudo de SQL.
            throw new InvalidOperationException(YaAbierto(caja, rival));
        }
    }

    /// <summary>Lo que debería haber en la gaveta: fondo + efectivo cobrado − vuelto, en Bs y en divisas.</summary>
    public (decimal Bs, decimal Divisa) Esperado(Turno turno)
    {
        var facturas = ventas.GetAll()
            .Where(v => v.TurnoId == turno.Id && v.Vigente && v.Tipo == TipoDocumentoVenta.Factura)
            .ToList();

        var bs = turno.FondoBs
                 + facturas.SelectMany(v => v.Pagos).Where(p => p.Metodo == MetodoPago.EfectivoBs).Sum(p => p.MontoBs)
                 - facturas.Sum(v => v.Vuelto);
        var divisa = turno.FondoDivisa
                     + facturas.SelectMany(v => v.Pagos).Where(p => p.Metodo == MetodoPago.EfectivoDivisa).Sum(p => p.MontoDivisa);

        return (bs, divisa);
    }

    public Turno Cerrar(Turno turno, decimal contadoBs, decimal contadoDivisa, string observaciones)
    {
        var usuario = sesion.UsuarioActual
            ?? throw new InvalidOperationException("No hay sesión iniciada.");

        var propio = turno.UsuarioId == usuario.Id;
        if (propio ? !sesion.Puede(Permisos.Turnos.Cerrar) : !sesion.Puede(Permisos.Turnos.CerrarAjeno))
            throw new InvalidOperationException(propio
                ? "No tienes permiso para cerrar turnos de caja."
                : $"El turno es de {turno.UsuarioNombre}. Solo un supervisor o un administrador puede cerrar el turno de otro cajero.");

        // Se relee: el turno de la pantalla puede estar viejo (lo cerró otra PC).
        var actual = turnos.GetById(turno.Id)
            ?? throw new InvalidOperationException("El turno ya no existe.");
        if (!actual.EstaAbierto)
            throw new InvalidOperationException($"El turno de {actual.UsuarioNombre} en {actual.CajaCodigo} ya estaba cerrado.");

        if (contadoBs < 0 || contadoDivisa < 0)
            throw new InvalidOperationException("Lo contado no puede ser negativo.");

        var (esperadoBs, esperadoDivisa) = Esperado(actual);

        actual.Estado = EstadoTurno.Cerrado;
        actual.Cierre = DateTime.Now;
        actual.EsperadoBs = esperadoBs;
        actual.EsperadoDivisa = esperadoDivisa;
        actual.ContadoBs = contadoBs;
        actual.ContadoDivisa = contadoDivisa;
        actual.CerradoPorId = usuario.Id;
        actual.CerradoPorNombre = usuario.NombreCompleto;
        actual.Observaciones = (observaciones ?? string.Empty).Trim();

        turnos.Update(actual);
        return actual;
    }

    private static string YaAbierto(Caja caja, Turno abierto) =>
        $"La caja {caja.Codigo} ya tiene un turno abierto por {abierto.UsuarioNombre} desde {abierto.AperturaTexto}. " +
        "Tiene que cerrarse antes de abrir otro.";
}
