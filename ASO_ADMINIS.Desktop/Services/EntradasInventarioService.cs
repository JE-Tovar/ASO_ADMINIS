using System;
using System.Collections.Generic;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Reglas de las entradas al almacén.
///
/// El <see cref="CuentasPorPagarService"/> es obligatorio por constructor, no opcional: recibir
/// mercancía comprada y deberla son la misma operación, igual que pagar una factura y asentar el
/// movimiento en el libro lo son en <see cref="CuentasPorPagarService.RegistrarPago"/>. Solo un
/// <see cref="TipoEntrada.Ajuste"/> se salta ese paso, porque no hay nadie a quien deberle.
/// </summary>
public sealed class EntradasInventarioService
{
    private const string Prefijo = "ENT-";

    private readonly IEntradaInventarioDataSource _entradas;
    private readonly IProveedorDataSource _proveedores;
    private readonly IFacturaProveedorDataSource _facturas;
    private readonly InventarioService _inventario;
    private readonly CuentasPorPagarService _cuentasPorPagar;
    private readonly ISesionActual _sesion;

    public EntradasInventarioService(IEntradaInventarioDataSource entradas,
                                     IProveedorDataSource proveedores,
                                     IFacturaProveedorDataSource facturas,
                                     InventarioService inventario,
                                     CuentasPorPagarService cuentasPorPagar,
                                     ISesionActual sesion)
    {
        _entradas = entradas;
        _proveedores = proveedores;
        _facturas = facturas;
        _inventario = inventario;
        _cuentasPorPagar = cuentasPorPagar;
        _sesion = sesion;
    }

    // --- Reglas de transición (alimentan el CanExecute) ---

    /// <summary>
    /// Puro y sin tocar la base: lo llama el <c>CanExecute</c> del botón, que se reevalúa
    /// constantemente. Que la factura generada esté ya pagada se comprueba dentro de
    /// <see cref="Anular"/>, no aquí.
    /// </summary>
    public bool PuedeAnular(EntradaInventario entrada) => entrada.Estado == EstadoEntrada.Registrada;

    // --- Validación ---

    public bool Validar(EntradaInventario entrada, out string? error)
    {
        if (entrada.Lineas.Count == 0)
        {
            error = "Agregue al menos un artículo a la entrada.";
            return false;
        }

        if (entrada.Lineas.Any(l => l.ArticuloId == 0))
        {
            error = "Hay una línea sin artículo seleccionado.";
            return false;
        }

        if (entrada.Lineas.Any(l => l.Cantidad <= 0))
        {
            error = "La cantidad de cada línea debe ser mayor que cero.";
            return false;
        }

        var repetido = entrada.Lineas
            .GroupBy(l => l.ArticuloId)
            .FirstOrDefault(g => g.Count() > 1);

        if (repetido is not null)
        {
            error = $"El artículo {repetido.First().ArticuloNombre} está en más de una línea; " +
                    "júntelas en una sola.";
            return false;
        }

        if (entrada.Tipo == TipoEntrada.CompraProveedor && entrada.ProveedorId is null or 0)
        {
            error = "Seleccione el proveedor al que se le compró.";
            return false;
        }

        if (entrada.Tipo == TipoEntrada.CompraExterna)
        {
            if (string.IsNullOrWhiteSpace(entrada.ProveedorNombre))
            {
                error = "Indique en qué comercio se hizo la compra.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(entrada.CompradoPor))
            {
                error = "Indique quién de la empresa hizo la compra.";
                return false;
            }
        }

        if (entrada.GeneraCuentaPorPagar)
        {
            if (string.IsNullOrWhiteSpace(entrada.NumeroDocumento))
            {
                error = "Indique el número de la factura o del recibo.";
                return false;
            }

            if (entrada.Lineas.Any(l => l.PrecioUnitario <= 0))
            {
                error = "Indique el precio de cada artículo comprado.";
                return false;
            }

            // La cuenta por pagar exige vencimiento; se comprueba aquí para dar el mensaje del
            // formulario que el usuario tiene delante, y no el de Finanzas.
            if (entrada.FechaVencimiento is not { } vencimiento || vencimiento.Date < entrada.Fecha.Date)
            {
                error = "El vencimiento no puede ser anterior a la fecha de la compra.";
                return false;
            }
        }

        error = null;
        return true;
    }

    // --- Transiciones ---

    /// <summary>
    /// Registra la entrada y, salvo que sea un ajuste, deja la cuenta por pagar en Finanzas.
    ///
    /// La factura va PRIMERO, por el mismo motivo que el asiento de banco va antes de marcar una
    /// factura como pagada: es la operación que puede rechazar (documento repetido del mismo
    /// proveedor). Si rechaza, la entrada no llega a escribirse y no queda nada a medias.
    /// </summary>
    public EntradaInventario Registrar(EntradaInventario entrada, int usuarioId)
    {
        if (entrada.Id != 0)
            throw new InvalidOperationException("Esta entrada ya está registrada.");

        if (!_sesion.Puede(Permisos.EntradasInventario.Crear))
            throw new InvalidOperationException("No tienes permiso para registrar entradas de almacén.");

        // El comercio de una compra externa se resuelve antes de validar, porque la validación de
        // la cuenta por pagar necesita saber a qué proveedor se le carga. Solo se da de alta si
        // no existía, y en ese caso no puede haber todavía una factura repetida suya.
        if (entrada.Tipo == TipoEntrada.CompraExterna && !string.IsNullOrWhiteSpace(entrada.ProveedorNombre))
        {
            var comercio = ResolverComercio(entrada.ProveedorNombre);
            entrada.ProveedorId = comercio.Id;
            entrada.ProveedorNombre = comercio.Nombre;
        }

        if (!Validar(entrada, out var error))
            throw new InvalidOperationException(error);

        entrada.Numero = SiguienteNumero();
        entrada.Estado = EstadoEntrada.Registrada;
        entrada.Total = entrada.Lineas.Sum(l => l.Subtotal);
        entrada.CreadoPorId = usuarioId;
        entrada.CreadoPorNombre = _sesion.UsuarioActual?.NombreCompleto ?? string.Empty;
        entrada.FechaCreacion = DateTime.Now;

        FacturaProveedor? factura = null;

        if (entrada.GeneraCuentaPorPagar)
        {
            factura = _cuentasPorPagar.Crear(ConstruirFactura(entrada), usuarioId);
            entrada.FacturaProveedorId = factura.Id;
            entrada.FacturaProveedorNumero = factura.NumeroDocumento;
        }

        try
        {
            return _entradas.Add(entrada);
        }
        catch
        {
            // No hay una transacción que abarque las dos tablas: cada fuente de datos abre su
            // propio contexto. Si la entrada no llegó a guardarse, se retira la factura recién
            // creada para no dejar una deuda sin la mercancía que la justifica.
            if (factura is not null)
                _facturas.Delete(factura.Id);

            throw;
        }
    }

    /// <summary>
    /// Deshace la entrada y, con ella, la cuenta por pagar que generó.
    ///
    /// Dos cosas pueden impedirlo, y las dos se comprueban antes de escribir nada: que lo que
    /// trajo esta entrada ya se haya despachado (el almacén quedaría en negativo) y que la
    /// factura ya esté pagada (eso se deshace con una nota de crédito, no desde el almacén).
    /// </summary>
    public EntradaInventario Anular(EntradaInventario entrada, string motivo)
    {
        if (!PuedeAnular(entrada))
            throw new InvalidOperationException("Solo se puede anular una entrada registrada.");

        if (!_sesion.Puede(Permisos.EntradasInventario.Anular))
            throw new InvalidOperationException("No tienes permiso para anular entradas de almacén.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la anulación.");

        var existencias = _inventario.ExistenciasPorArticulo();

        foreach (var linea in entrada.Lineas)
        {
            var quedaria = existencias.GetValueOrDefault(linea.ArticuloId) - linea.Cantidad;

            if (quedaria < 0)
                throw new InvalidOperationException(
                    $"No se puede anular: de {linea.ArticuloNombre} ya se despacharon " +
                    $"{-quedaria:N2} {linea.UnidadTexto} de los que trajo esta entrada.");
        }

        // La factura va primero: es la que puede rechazar.
        if (entrada.FacturaProveedorId is { } facturaId && _facturas.GetById(facturaId) is { } factura)
        {
            if (factura.Estado == EstadoFacturaProveedor.Pagada)
                throw new InvalidOperationException(
                    $"La factura Nº {factura.NumeroDocumento} que generó esta entrada ya fue pagada; " +
                    "registre la nota de crédito del proveedor antes de anular la entrada.");

            if (factura.Estado == EstadoFacturaProveedor.Pendiente)
                _cuentasPorPagar.Anular(factura, $"Entrada {entrada.Numero} anulada: {motivo.Trim()}");
        }

        var copia = entrada.Clonar();
        copia.Estado = EstadoEntrada.Anulada;
        copia.MotivoAnulacion = motivo.Trim();
        copia.FechaAnulacion = DateTime.Now;
        _entradas.Update(copia);
        return copia;
    }

    // --- Piezas internas ---

    /// <summary>
    /// El comercio de una compra externa entra al padrón de proveedores como uno más: la deuda
    /// tiene que quedar a nombre de alguien, y así se paga por el mismo camino de siempre.
    /// Se busca por nombre; solo se da de alta si de verdad no estaba.
    /// </summary>
    private Proveedor ResolverComercio(string nombre)
    {
        var buscado = nombre.Trim();

        var existente = _proveedores.GetAll()
            .FirstOrDefault(p => string.Equals(p.Nombre.Trim(), buscado, StringComparison.OrdinalIgnoreCase));

        if (existente is not null)
            return existente;

        if (!_sesion.Puede(Permisos.Proveedores.Crear))
            throw new InvalidOperationException(
                $"El comercio {buscado} no está en el padrón de proveedores y no tienes permiso " +
                "para darlo de alta.");

        return _proveedores.Add(new Proveedor
        {
            Nombre = buscado,
            Activo = true,
            Notas = "Alta automática desde una compra externa de almacén."
        });
    }

    /// <summary>
    /// Arma la cuenta por pagar a partir de la entrada. El detalle de la factura sale de las
    /// líneas del documento de almacén: es exactamente lo que se compró y a qué precio.
    /// </summary>
    private static FacturaProveedor ConstruirFactura(EntradaInventario entrada) => new()
    {
        ProveedorId = entrada.ProveedorId!.Value,
        ProveedorNombre = entrada.ProveedorNombre,
        NumeroDocumento = entrada.NumeroDocumento.Trim(),
        Descripcion = entrada.Tipo == TipoEntrada.CompraExterna
            ? $"Entrada de almacén {entrada.Numero} · compra externa realizada por {entrada.CompradoPor}"
            : $"Entrada de almacén {entrada.Numero}",
        FechaEmision = entrada.Fecha,
        FechaVencimiento = entrada.FechaVencimiento,
        Monto = entrada.Lineas.Sum(l => l.Subtotal),
        Lineas = entrada.Lineas.Select(l => new FacturaProveedorLinea
        {
            DestinoTexto = $"{l.ArticuloCodigo} · {l.ArticuloNombre}",
            CantidadTexto = l.CantidadTexto,
            PrecioUnitario = l.PrecioUnitario,
            Subtotal = l.Subtotal
        }).ToList()
    };

    /// <summary>
    /// Correlativo del almacén. Se calcula al registrar, no en el formulario, y el índice único
    /// <c>(OrganizacionId, Numero)</c> es la red por si dos puestos coincidieran.
    /// </summary>
    private string SiguienteNumero()
    {
        var ultimo = _entradas.GetAll()
            .Select(e => e.Numero)
            .Where(n => n.StartsWith(Prefijo, StringComparison.Ordinal))
            .Select(n => int.TryParse(n[Prefijo.Length..], out var valor) ? valor : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{Prefijo}{ultimo + 1:D6}";
    }

    // --- Resúmenes para el panel del módulo ---

    public IReadOnlyList<EntradaInventario> DelMes()
    {
        var desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        return [.. _entradas.GetAll()
            .Where(e => e.Estado == EstadoEntrada.Registrada && e.Fecha.Date >= desde)];
    }

    public decimal TotalComprasDelMes() => DelMes().Where(e => e.GeneraCuentaPorPagar).Sum(e => e.Total);
}
