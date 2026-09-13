using System;
using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Reglas de los boletos de salida.
///
/// Más simple que su contraparte de entradas: una salida no le debe nada a nadie, así que no
/// arrastra ningún documento de otro módulo. Lo único que no puede pasar es que el almacén quede
/// en negativo, y esa es la regla que este servicio defiende.
/// </summary>
public sealed class SalidasInventarioService
{
    private const string Prefijo = "SAL-";

    private readonly ISalidaInventarioDataSource _salidas;
    private readonly InventarioService _inventario;
    private readonly ISesionActual _sesion;

    public SalidasInventarioService(ISalidaInventarioDataSource salidas,
                                    InventarioService inventario,
                                    ISesionActual sesion)
    {
        _salidas = salidas;
        _inventario = inventario;
        _sesion = sesion;
    }

    // --- Reglas de transición (alimentan el CanExecute) ---

    public bool PuedeAnular(SalidaInventario salida) => salida.Estado == EstadoSalida.Registrada;

    // --- Validación ---

    /// <summary>
    /// Valida el boleto contra la existencia REAL del momento, no contra la que tenía el
    /// formulario al abrirse: entre que se abre el modal y se guarda, otro puesto pudo haber
    /// sacado material. La pantalla avisa antes, pero quien manda es esta comprobación.
    /// </summary>
    public bool Validar(SalidaInventario salida, out string? error)
    {
        if (salida.Lineas.Count == 0)
        {
            error = "Agregue al menos un artículo al boleto.";
            return false;
        }

        if (salida.Lineas.Any(l => l.ArticuloId == 0))
        {
            error = "Hay una línea sin artículo seleccionado.";
            return false;
        }

        if (salida.Lineas.Any(l => l.Cantidad <= 0))
        {
            error = "La cantidad de cada línea debe ser mayor que cero.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(salida.RetiradoPor))
        {
            error = "Indique quién retira el material.";
            return false;
        }

        if (salida.Destino == AreaDestino.Otro && string.IsNullOrWhiteSpace(salida.DestinoDetalle))
        {
            error = "Describa a dónde va el material.";
            return false;
        }

        var existencias = _inventario.ExistenciasPorArticulo();

        // Se agrupa por artículo antes de comparar: dos líneas del mismo artículo que por
        // separado caben, juntas pueden no caber.
        foreach (var grupo in salida.Lineas.GroupBy(l => l.ArticuloId))
        {
            var pedido = grupo.Sum(l => l.Cantidad);
            var disponible = existencias.GetValueOrDefault(grupo.Key);

            if (pedido > disponible)
            {
                var linea = grupo.First();
                error = $"No hay existencia suficiente de {linea.ArticuloNombre}: " +
                        $"hay {disponible:N2} {linea.UnidadTexto} y se piden {pedido:N2}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    // --- Transiciones ---

    /// <summary>
    /// Emite el boleto. Quién autoriza no es un campo del formulario: es quien tiene la sesión
    /// abierta, y se estampa aquí para que no se pueda escribir otro nombre.
    /// </summary>
    public SalidaInventario Registrar(SalidaInventario salida, int usuarioId)
    {
        if (salida.Id != 0)
            throw new InvalidOperationException("Este boleto ya está emitido.");

        if (!_sesion.Puede(Permisos.SalidasInventario.Crear))
            throw new InvalidOperationException("No tienes permiso para registrar salidas de almacén.");

        if (!Validar(salida, out var error))
            throw new InvalidOperationException(error);

        salida.Numero = SiguienteNumero();
        salida.Estado = EstadoSalida.Registrada;
        salida.AutorizadoPorId = usuarioId;
        salida.AutorizadoPorNombre = _sesion.UsuarioActual?.NombreCompleto ?? string.Empty;
        salida.CreadoPorId = usuarioId;
        salida.FechaCreacion = DateTime.Now;

        return _salidas.Add(salida);
    }

    /// <summary>
    /// Anula el boleto. La existencia vuelve sola: el kardex no cuenta los documentos anulados,
    /// así que no hay que devolver nada a mano.
    /// </summary>
    public SalidaInventario Anular(SalidaInventario salida, string motivo)
    {
        if (!PuedeAnular(salida))
            throw new InvalidOperationException("Solo se puede anular un boleto registrado.");

        if (!_sesion.Puede(Permisos.SalidasInventario.Anular))
            throw new InvalidOperationException("No tienes permiso para anular salidas de almacén.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la anulación.");

        var copia = salida.Clonar();
        copia.Estado = EstadoSalida.Anulada;
        copia.MotivoAnulacion = motivo.Trim();
        copia.FechaAnulacion = DateTime.Now;
        _salidas.Update(copia);
        return copia;
    }

    // --- Piezas internas ---

    /// <summary>
    /// Correlativo del boleto. Mismo criterio que el de las entradas: se calcula al emitir y el
    /// índice único <c>(OrganizacionId, Numero)</c> es la red por si dos puestos coincidieran.
    /// </summary>
    private string SiguienteNumero()
    {
        var ultimo = _salidas.GetAll()
            .Select(s => s.Numero)
            .Where(n => n.StartsWith(Prefijo, StringComparison.Ordinal))
            .Select(n => int.TryParse(n[Prefijo.Length..], out var valor) ? valor : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{Prefijo}{ultimo + 1:D6}";
    }

    // --- Resúmenes para el panel del módulo ---

    public IReadOnlyList<SalidaInventario> DelMes()
    {
        var desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        return [.. _salidas.GetAll()
            .Where(s => s.Estado == EstadoSalida.Registrada && s.Fecha.Date >= desde)];
    }
}
