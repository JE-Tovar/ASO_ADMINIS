using System;
using System.Linq;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Reglas de las cuentas por pagar. La factura de compra se registra tal como la emitió el
/// proveedor, así que aquí no hay generación: hay validación (que el documento no se cargue dos
/// veces) y dos transiciones, pagar y anular.
/// </summary>
public sealed class CuentasPorPagarService
{
    private readonly IFacturaProveedorDataSource _facturas;
    private readonly BancoService _banco;
    private readonly ISesionActual _sesion;

    /// <summary>
    /// El <see cref="BancoService"/> es obligatorio: pagar la factura y anotar la salida en el
    /// libro son la misma operación (ver <see cref="RegistrarPago"/>).
    /// </summary>
    public CuentasPorPagarService(IFacturaProveedorDataSource facturas, BancoService banco, ISesionActual sesion)
    {
        _facturas = facturas;
        _banco = banco;
        _sesion = sesion;
    }

    // --- Reglas de transición (alimentan el CanExecute) ---

    public bool PuedeEditar(FacturaProveedor f) => f.Estado == EstadoFacturaProveedor.Pendiente;

    public bool PuedeEliminar(FacturaProveedor f) => f.Estado == EstadoFacturaProveedor.Pendiente;

    public bool PuedeRegistrarPago(FacturaProveedor f) => f.Estado == EstadoFacturaProveedor.Pendiente;

    public bool PuedeAnular(FacturaProveedor f) => f.Estado == EstadoFacturaProveedor.Pendiente;

    /// <summary>
    /// Valida antes de guardar. El número de documento no puede repetirse dentro del mismo
    /// proveedor: es el control que evita pagar dos veces la misma factura.
    /// </summary>
    public bool Validar(FacturaProveedor factura, out string? error)
    {
        if (factura.ProveedorId == 0)
        {
            error = "Seleccione el proveedor que emitió la factura.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(factura.NumeroDocumento))
        {
            error = "Indique el número de la factura del proveedor.";
            return false;
        }

        if (factura.Monto <= 0)
        {
            error = "El monto de la factura debe ser mayor que cero.";
            return false;
        }

        if (factura.FechaVencimiento is not { } vencimiento || vencimiento.Date < factura.FechaEmision.Date)
        {
            error = "El vencimiento no puede ser anterior a la fecha de emisión.";
            return false;
        }

        var repetida = _facturas.GetByProveedor(factura.ProveedorId)
            .Where(f => f.Id != factura.Id && f.Estado != EstadoFacturaProveedor.Anulada)
            .Any(f => string.Equals(f.NumeroDocumento.Trim(), factura.NumeroDocumento.Trim(),
                                    StringComparison.OrdinalIgnoreCase));

        if (repetida)
        {
            error = $"El proveedor {factura.ProveedorNombre} ya tiene registrada la factura " +
                    $"Nº {factura.NumeroDocumento.Trim()}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Alta de una cuenta por pagar que NACE DE OTRO MÓDULO (hoy, de una entrada de almacén),
    /// no de un formulario de Finanzas.
    ///
    /// Existe para que ese camino pase por la misma validación que el alta manual —sobre todo
    /// por el control de número repetido, que es lo que evita que la misma factura del proveedor
    /// se cargue dos veces— en vez de escribir directo contra la fuente de datos.
    ///
    /// No vuelve a comprobar el permiso: lo exigió quien llama, con el permiso de SU documento
    /// (<see cref="Permisos.EntradasInventario.Crear"/>). Es el mismo criterio con el que
    /// <see cref="BancoService"/> no repite el chequeo de <c>Finanzas.Pagar</c> al asentar un pago.
    /// </summary>
    public FacturaProveedor Crear(FacturaProveedor factura, int usuarioId)
    {
        if (!Validar(factura, out var error))
            throw new InvalidOperationException(error);

        factura.Estado = EstadoFacturaProveedor.Pendiente;
        factura.CreadoPorId = usuarioId;
        factura.FechaCreacion = DateTime.Now;
        return _facturas.Add(factura);
    }

    /// <summary>
    /// Da la factura por pagada y anota la salida en el libro de banco, en una sola operación. El
    /// asiento va primero porque es el que puede rechazar; ver
    /// <see cref="FacturaClienteService.RegistrarCobro"/>.
    /// </summary>
    public FacturaProveedor RegistrarPago(FacturaProveedor factura, AsientoBanco asiento, int usuarioId)
    {
        if (!PuedeRegistrarPago(factura))
            throw new InvalidOperationException("Solo se puede pagar una factura pendiente.");

        if (!_sesion.Puede(Permisos.Finanzas.Pagar))
            throw new InvalidOperationException("No tienes permiso para pagar facturas de proveedor.");

        _banco.RegistrarPagoProveedor(factura, asiento, usuarioId);

        var copia = factura.Clonar();
        copia.Estado = EstadoFacturaProveedor.Pagada;
        copia.FechaPago = DateTime.Today;
        _facturas.Update(copia);
        return copia;
    }

    public FacturaProveedor Anular(FacturaProveedor factura, string motivo)
    {
        if (!PuedeAnular(factura))
            throw new InvalidOperationException(
                factura.Estado == EstadoFacturaProveedor.Pagada
                    ? "Una factura ya pagada no se anula: registre la nota de crédito del proveedor."
                    : "Solo se puede anular una factura pendiente.");

        if (!_sesion.Puede(Permisos.Finanzas.Anular))
            throw new InvalidOperationException("No tienes permiso para anular facturas de proveedor.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la anulación.");

        var copia = factura.Clonar();
        copia.Estado = EstadoFacturaProveedor.Anulada;
        copia.MotivoAnulacion = motivo.Trim();
        copia.FechaAnulacion = DateTime.Now;
        _facturas.Update(copia);
        return copia;
    }

    /// <summary>Deuda pendiente del centro.</summary>
    public decimal TotalPorPagar() =>
        _facturas.GetAll()
            .Where(f => f.Estado == EstadoFacturaProveedor.Pendiente)
            .Sum(f => f.Monto);

    public decimal TotalVencido() =>
        _facturas.GetAll()
            .Where(f => f.EstaVencida)
            .Sum(f => f.Monto);
}
