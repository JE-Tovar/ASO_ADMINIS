using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Cálculo de los montos de un documento fiscal. Puro (no toca la base), para que el editor lo
/// use en vivo y el servicio lo repita al emitir con los mismos números.
///
/// Reglas:
/// - Cada renglón se redondea a 2 decimales: precio Bs = precio US$ × tasa; monto = cantidad ×
///   precio Bs; IVA del renglón = monto × % de su alícuota.
/// - Las bases e IVA por alícuota son la suma de sus renglones (así cuadran con lo impreso).
/// - IGTF (solo si la organización es contribuyente especial): % sobre lo cobrado en divisas,
///   en Bs, con tope en el total antes del IGTF — pagar de más en dólares no crea más impuesto.
/// </summary>
public static class CalculoFiscal
{
    public static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    /// <summary>Completa precio en Bs, monto, porcentaje e IVA de un renglón.</summary>
    public static void CalcularLinea(VentaLinea linea, Organizacion organizacion, decimal tasa, bool precioEnUsd = true)
    {
        if (precioEnUsd)
            linea.PrecioUnitario = Redondear(linea.PrecioUnitarioUsd * tasa);

        linea.PorcentajeIva = Alicuotas.Porcentaje(linea.Alicuota, organizacion);
        linea.Monto = Redondear(linea.Cantidad * linea.PrecioUnitario);
        linea.Iva = Redondear(linea.Monto * linea.PorcentajeIva / 100m);
    }

    /// <summary>
    /// Totaliza un documento cuyos renglones ya están calculados. Los porcentajes de cabecera
    /// se toman de la organización; un renglón que traiga otro porcentaje (nota de crédito de
    /// una factura emitida con otra alícuota vigente) se respeta en su IVA.
    /// </summary>
    public static void Totalizar(Venta documento, Organizacion organizacion, bool percibeIgtf)
    {
        decimal Base(TipoAlicuota a) => documento.Lineas.Where(l => l.Alicuota == a).Sum(l => l.Monto);
        decimal Iva(TipoAlicuota a) => documento.Lineas.Where(l => l.Alicuota == a).Sum(l => l.Iva);
        decimal Porcentaje(TipoAlicuota a) =>
            documento.Lineas.FirstOrDefault(l => l.Alicuota == a)?.PorcentajeIva
            ?? Alicuotas.Porcentaje(a, organizacion);

        documento.MontoExento = Base(TipoAlicuota.Exento);
        documento.BaseGeneral = Base(TipoAlicuota.General);
        documento.IvaGeneral = Iva(TipoAlicuota.General);
        documento.PorcentajeGeneral = Porcentaje(TipoAlicuota.General);
        documento.BaseReducida = Base(TipoAlicuota.Reducida);
        documento.IvaReducida = Iva(TipoAlicuota.Reducida);
        documento.PorcentajeReducida = Porcentaje(TipoAlicuota.Reducida);
        documento.BaseAdicional = Base(TipoAlicuota.Adicional);
        documento.IvaAdicional = Iva(TipoAlicuota.Adicional);
        documento.PorcentajeAdicional = Porcentaje(TipoAlicuota.Adicional);

        var totalConIva = documento.MontoExento + documento.BaseImponible + documento.TotalIva;

        // IGTF: solo cuando la organización percibe y solo en lo pagado en divisas.
        var enDivisas = documento.Pagos.Where(p => p.EsDivisa).Sum(p => p.MontoBs);
        documento.PorcentajeIgtf = percibeIgtf ? organizacion.IgtfPorcentaje : 0m;
        documento.BaseIgtf = percibeIgtf ? Math.Min(enDivisas, totalConIva) : 0m;
        documento.Igtf = Redondear(documento.BaseIgtf * documento.PorcentajeIgtf / 100m);

        documento.Total = totalConIva + documento.Igtf;

        var pagado = documento.Pagos.Sum(p => p.MontoBs);
        documento.Vuelto = pagado > documento.Total ? pagado - documento.Total : 0m;
    }

    /// <summary>Lo que falta por cobrar (positivo) o sobra (negativo) con los pagos cargados.</summary>
    public static decimal Pendiente(Venta documento) =>
        documento.Total - documento.Pagos.Sum(p => p.MontoBs);
}
