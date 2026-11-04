using System;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Reglas de la venta: valida contra la existencia real y, al registrar, genera el
/// <see cref="SalidaInventario"/> vinculado para que el kardex la cuente — mismo acoplamiento
/// entre módulos que <c>EntradasInventarioService</c> exigiendo <c>CuentasPorPagarService</c>.
/// Lo que se vende es directamente un <see cref="Articulo"/> del almacén.
///
/// No pasa por <see cref="SalidasInventarioService"/>: sus reglas (quién retira, a qué área)
/// son de un boleto de almacén, no de una venta. Aquí se escribe la Salida directo contra su
/// fuente de datos, con valores sensatos (ver <see cref="Registrar"/>).
/// </summary>
public sealed class VentasService
{
    private const string Prefijo = "VTA-";
    private const string PrefijoSalida = "SAL-";

    private readonly IVentaDataSource _ventas;
    private readonly IArticuloDataSource _articulos;
    private readonly ISalidaInventarioDataSource _salidas;
    private readonly InventarioService _inventario;
    private readonly ISesionActual _sesion;

    public VentasService(IVentaDataSource ventas,
                         IArticuloDataSource articulos,
                         ISalidaInventarioDataSource salidas,
                         InventarioService inventario,
                         ISesionActual sesion)
    {
        _ventas = ventas;
        _articulos = articulos;
        _salidas = salidas;
        _inventario = inventario;
        _sesion = sesion;
    }

    public bool PuedeAnular(Venta venta) => venta.Estado == EstadoVenta.Registrada;

    /// <summary>
    /// Valida contra la existencia REAL del momento, no la que tenía el formulario al abrirse
    /// — mismo criterio que <c>SalidasInventarioService.Validar</c>.
    /// </summary>
    public bool Validar(Venta venta, out string? error)
    {
        if (venta.ArticuloId == 0)
        {
            error = "Elija el artículo que se vendió.";
            return false;
        }

        if (venta.Cantidad <= 0)
        {
            error = "La cantidad debe ser mayor que cero.";
            return false;
        }

        if (venta.PrecioUnitarioUsd < 0)
        {
            error = "El precio no puede ser negativo.";
            return false;
        }

        if (venta.MetodoPago is null)
        {
            error = "Indique el método de pago.";
            return false;
        }

        var articulo = _articulos.GetById(venta.ArticuloId);
        if (articulo is null)
        {
            error = "El artículo elegido ya no existe en el almacén.";
            return false;
        }

        if (venta.Factor <= 0)
        {
            error = "Elija la presentación que se vendió.";
            return false;
        }

        // Se compara en unidad base, que es en la que se lleva la existencia.
        var disponible = _inventario.Existencia(articulo.Id);
        if (venta.CantidadBase > disponible)
        {
            var enPresentacion = venta.Factor == 1m
                ? string.Empty
                : $" (≈ {disponible / venta.Factor:N2} {venta.PresentacionNombre})";

            error = $"No hay existencia suficiente de {articulo.Nombre}: " +
                    $"hay {disponible:N2} {articulo.UnidadCorta}{enPresentacion} " +
                    $"y se piden {venta.CantidadTexto}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Registra la venta y, además, escribe la Salida de almacén vinculada para que el kardex
    /// la cuente. Las dos escrituras no comparten transacción (cada fuente abre su propio
    /// contexto, igual que en el resto del scaffold): si la Salida fallara, la Venta ya guardada
    /// quedaría sin su contraparte de almacén — riesgo aceptado, mismo criterio que Entradas con
    /// su factura de Finanzas.
    /// </summary>
    public Venta Registrar(Venta venta, int usuarioId)
    {
        if (venta.Id != 0)
            throw new InvalidOperationException("Esta venta ya está registrada.");

        if (!_sesion.Puede(Permisos.Ventas.Crear))
            throw new InvalidOperationException("No tienes permiso para registrar ventas.");

        if (!Validar(venta, out var error))
            throw new InvalidOperationException(error);

        var articulo = _articulos.GetById(venta.ArticuloId)!;

        venta.Numero = SiguienteNumeroVenta();
        venta.Estado = EstadoVenta.Registrada;
        venta.CreadoPorId = usuarioId;
        venta.FechaCreacion = DateTime.Now;

        var guardada = _ventas.Add(venta);

        _salidas.Add(new SalidaInventario
        {
            Numero = SiguienteNumeroSalida(),
            Fecha = guardada.Fecha,
            Destino = AreaDestino.Venta,
            Motivo = MotivoSalida.Venta,
            RetiradoPor = string.IsNullOrWhiteSpace(guardada.ClienteNombre) ? "Cliente" : guardada.ClienteNombre,
            AutorizadoPorId = usuarioId,
            AutorizadoPorNombre = _sesion.UsuarioActual?.NombreCompleto ?? string.Empty,
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.Now,
            Estado = EstadoSalida.Registrada,
            VentaId = guardada.Id,
            Lineas =
            [
                new SalidaInventarioLinea
                {
                    ArticuloId = articulo.Id,
                    ArticuloCodigo = articulo.Codigo,
                    ArticuloNombre = articulo.Nombre,
                    UnidadTexto = articulo.UnidadCorta,
                    // En unidad base: 3 galones salen del almacén como 11,355 L.
                    Cantidad = Math.Round(guardada.CantidadBase, 4)
                }
            ]
        });

        return guardada;
    }

    /// <summary>Anula la venta y, si sigue registrada, también su Salida vinculada, para que la existencia vuelva sola.</summary>
    public Venta Anular(Venta venta, string motivo)
    {
        if (!PuedeAnular(venta))
            throw new InvalidOperationException("Solo se puede anular una venta registrada.");

        if (!_sesion.Puede(Permisos.Ventas.Anular))
            throw new InvalidOperationException("No tienes permiso para anular ventas.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la anulación.");

        if (_salidas.GetAll().FirstOrDefault(s => s.VentaId == venta.Id) is { Estado: EstadoSalida.Registrada } salida)
        {
            var copiaSalida = salida.Clonar();
            copiaSalida.Estado = EstadoSalida.Anulada;
            copiaSalida.MotivoAnulacion = $"Venta {venta.Numero} anulada: {motivo.Trim()}";
            copiaSalida.FechaAnulacion = DateTime.Now;
            _salidas.Update(copiaSalida);
        }

        var copia = venta.Clonar();
        copia.Estado = EstadoVenta.Anulada;
        copia.MotivoAnulacion = motivo.Trim();
        copia.FechaAnulacion = DateTime.Now;
        _ventas.Update(copia);
        return copia;
    }

    private string SiguienteNumeroVenta()
    {
        var ultimo = _ventas.GetAll()
            .Select(v => v.Numero)
            .Where(n => n.StartsWith(Prefijo, StringComparison.Ordinal))
            .Select(n => int.TryParse(n[Prefijo.Length..], out var valor) ? valor : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{Prefijo}{ultimo + 1:D6}";
    }

    private string SiguienteNumeroSalida()
    {
        var ultimo = _salidas.GetAll()
            .Select(s => s.Numero)
            .Where(n => n.StartsWith(PrefijoSalida, StringComparison.Ordinal))
            .Select(n => int.TryParse(n[PrefijoSalida.Length..], out var valor) ? valor : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{PrefijoSalida}{ultimo + 1:D6}";
    }
}
