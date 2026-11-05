using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Registro de los comprobantes de retención de IVA que entregan los clientes agentes de
/// retención. La factura no se toca (es inalterable): el comprobante es una fila aparte que el
/// libro de ventas cruza con ella.
/// </summary>
public sealed class RetencionesService(IRetencionIvaDataSource retenciones, ISesionActual sesion)
{
    public static readonly decimal[] PorcentajesValidos = [75m, 100m];

    public RetencionIvaRecibida? DeLaFactura(int ventaId) =>
        retenciones.GetAll().FirstOrDefault(r => r.VentaId == ventaId);

    public bool PuedeRegistrar(Venta? factura) =>
        factura is { Tipo: TipoDocumentoVenta.Factura, EsHistorico: false }
        && factura.Vigente
        && Rif.EsRifValido(factura.ClienteRif)
        && factura.TotalIva > 0;

    /// <summary>Monto retenido: el % sobre el IVA de la factura, redondeado al céntimo.</summary>
    public static decimal Calcular(Venta factura, decimal porcentaje) =>
        CalculoFiscal.Redondear(factura.TotalIva * porcentaje / 100m);

    public RetencionIvaRecibida Registrar(Venta factura, string numeroComprobante, DateTime fecha, decimal porcentaje, int usuarioId)
    {
        if (!sesion.Puede(Permisos.Ventas.Retenciones))
            throw new InvalidOperationException("No tienes permiso para registrar retenciones.");

        if (!PuedeRegistrar(factura))
            throw new InvalidOperationException(
                "Solo se registra una retención sobre una factura fiscal vigente, con IVA, a un cliente con RIF.");

        var numero = new string((numeroComprobante ?? string.Empty).Where(char.IsDigit).ToArray());
        if (numero.Length != 14)
            throw new InvalidOperationException(
                "El número de comprobante tiene 14 dígitos: año y mes (AAAAMM) más un correlativo de 8.");

        if (!PorcentajesValidos.Contains(porcentaje))
            throw new InvalidOperationException("La retención de IVA es del 75 % o del 100 %.");

        if (fecha.Date < factura.Fecha.Date)
            throw new InvalidOperationException("El comprobante no puede ser anterior a la factura.");

        if (DeLaFactura(factura.Id) is not null)
            throw new InvalidOperationException($"La factura {factura.NumeroFiscal} ya tiene su retención registrada.");

        if (retenciones.GetAll().Any(r => r.ClienteRif == factura.ClienteRif && r.NumeroComprobante == numero))
            throw new InvalidOperationException($"El comprobante {numero} de {factura.ClienteRif} ya está registrado.");

        return retenciones.Add(new RetencionIvaRecibida
        {
            VentaId = factura.Id,
            VentaNumeroFiscal = factura.NumeroFiscal,
            ClienteRif = factura.ClienteRif,
            ClienteNombre = factura.ClienteNombre,
            NumeroComprobante = numero,
            FechaComprobante = fecha.Date,
            Porcentaje = porcentaje,
            IvaFactura = factura.TotalIva,
            MontoRetenido = Calcular(factura, porcentaje),
            CreadoPorId = usuarioId,
            FechaCreacion = DateTime.Now
        });
    }
}
