using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Los textos de una venta que comparten la ficha (<c>VentaDetalleViewModel</c>) y el comprobante
/// no fiscal, para que no digan cosas distintas.
/// </summary>
public static class TextosDeVenta
{
    /// <summary>Las filas del desglose fiscal que tienen monto: exento, cada alícuota, IGTF y vuelto.</summary>
    public static IReadOnlyList<(string Concepto, decimal Monto)> Totales(Venta v)
    {
        var filas = new List<(string, decimal)>();
        if (v.MontoExento != 0) filas.Add(("Exento", v.MontoExento));
        if (v.BaseGeneral != 0) filas.Add(($"Base imponible {v.PorcentajeGeneral:0.##} %", v.BaseGeneral));
        if (v.IvaGeneral != 0) filas.Add(($"IVA {v.PorcentajeGeneral:0.##} %", v.IvaGeneral));
        if (v.BaseReducida != 0) filas.Add(($"Base imponible {v.PorcentajeReducida:0.##} %", v.BaseReducida));
        if (v.IvaReducida != 0) filas.Add(($"IVA {v.PorcentajeReducida:0.##} %", v.IvaReducida));
        if (v.BaseAdicional != 0) filas.Add(($"Base imponible {v.PorcentajeAdicional:0.##} %", v.BaseAdicional));
        if (v.IvaAdicional != 0) filas.Add(($"IVA {v.PorcentajeAdicional:0.##} %", v.IvaAdicional));
        if (v.Igtf != 0) filas.Add(($"IGTF {v.PorcentajeIgtf:0.##} % (sobre {v.BaseIgtf:N2})", v.Igtf));
        if (v.Vuelto != 0) filas.Add(("Vuelto", v.Vuelto));
        return filas;
    }

    /// <summary>"Factura N° F00000012 del 03/11/2026" en una nota; vacío en una factura.</summary>
    public static string DocumentoAfectado(Venta v) => string.IsNullOrWhiteSpace(v.DocumentoAfectadoNumero)
        ? string.Empty
        : v.DocumentoAfectadoFecha is { } fecha
            ? $"Factura N° {v.DocumentoAfectadoNumero} del {fecha:dd/MM/yyyy}"
            : $"Factura N° {v.DocumentoAfectadoNumero}";

    public static string ClienteIdentificacion(Venta v) => v.EsConsumidorFinal ? "Consumidor final" : v.ClienteRif;
}

/// <summary>
/// Una copia NO FISCAL de una factura o nota ya emitida, para imprimirla o mandarla en PDF al
/// cliente. El documento con validez fiscal es el del emisor (lo imprime la máquina fiscal o lo
/// devuelve la imprenta digital); este lo dice en grande para que nunca se confundan.
///
/// Es solo datos ya formateados: lo dibujan <see cref="ComprobantePdf"/> (PDF) y
/// <see cref="ComprobanteImpreso"/> (impresora), así los dos muestran exactamente lo mismo.
///
/// Los datos del emisor salen de la organización ACTUAL: la venta no guarda una copia de ellos.
/// Si la razón social o la dirección cambiaron después de emitir, la copia muestra la nueva.
/// </summary>
public sealed record ComprobanteNoFiscal(
    string EmisorRazonSocial,
    string EmisorRif,
    string EmisorDireccion,
    string EmisorTelefono,
    string Titulo,
    string Banda,
    IReadOnlyList<(string Etiqueta, string Valor)> Datos,
    IReadOnlyList<(string Etiqueta, string Valor)> Cliente,
    IReadOnlyList<ComprobanteNoFiscal.Renglon> Renglones,
    IReadOnlyList<(string Concepto, string Monto)> Totales,
    string Total,
    string TotalUsd,
    IReadOnlyList<(string Metodo, string Monto, string Referencia)> Pagos,
    IReadOnlyList<string> Pie,
    string NombreArchivo)
{
    public const string TextoBanda = "DOCUMENTO NO FISCAL · Copia para el cliente";

    public sealed record Renglon(string Cantidad, string Descripcion, string PrecioUnitario, string Alicuota, string Monto);

    public static ComprobanteNoFiscal De(Venta v, Organizacion organizacion, string emisorNombre, bool emisorEsFiscal, DateTime? ahora = null)
    {
        var titulo = v.EsHistorico
            ? $"Venta (histórico) {v.Numero}"
            : string.IsNullOrWhiteSpace(v.NumeroFiscal)
                ? $"{v.TipoTexto} {v.Numero}"
                : $"{v.TipoTexto} N° {v.NumeroFiscal}";

        var datos = new List<(string, string)>
        {
            ("N° de control", Valor(v.NumeroControl)),
            ("Fecha y hora", v.FechaHoraTexto),
            ("Caja", Valor(v.CajaCodigo)),
            ("N° interno", v.Numero)
        };
        if (v.EnContingencia)
            datos.Add(("Emisión", "En formato de contingencia"));
        if (TextosDeVenta.DocumentoAfectado(v) is { Length: > 0 } afectado)
            datos.Add(("Documento afectado", afectado));
        if (!string.IsNullOrWhiteSpace(v.Concepto))
            datos.Add(("Concepto", v.Concepto));
        if (v.TasaCambioUsada > 0)
            datos.Add(("Tasa de cambio", $"Bs. {v.TasaCambioUsada:N2} por US$"));

        var cliente = new List<(string, string)>
        {
            ("RIF / C.I.", TextosDeVenta.ClienteIdentificacion(v)),
            ("Nombre o razón social", Valor(v.EsConsumidorFinal && string.IsNullOrWhiteSpace(v.ClienteNombre) ? "Consumidor final" : v.ClienteNombre))
        };
        if (!string.IsNullOrWhiteSpace(v.ClienteDireccion)) cliente.Add(("Dirección", v.ClienteDireccion));
        if (!string.IsNullOrWhiteSpace(v.ClienteTelefono)) cliente.Add(("Teléfono", v.ClienteTelefono));
        if (!string.IsNullOrWhiteSpace(v.ClienteCorreo)) cliente.Add(("Correo", v.ClienteCorreo));

        var renglones = v.Lineas.Select(l => new Renglon(
            l.CantidadTexto,
            string.IsNullOrWhiteSpace(l.Codigo) ? l.Descripcion : $"{l.Descripcion} ({l.Codigo})",
            l.PrecioUnitarioTexto,
            l.AlicuotaTexto,
            l.MontoTexto)).ToList();

        var pagos = v.Pagos.Select(p => (
            p.MetodoTexto,
            p.EsDivisa && p.MontoDivisa > 0 ? $"{p.MontoBs:N2}  (US$ {p.MontoDivisa:N2})" : p.MontoBs.ToString("N2"),
            p.Referencia)).ToList();

        var emitidoPor = string.IsNullOrWhiteSpace(v.NumeroFiscal)
            ? "Este documento no tiene validez fiscal."
            : $"Este documento no tiene validez fiscal. El documento fiscal es el emitido por {emisorNombre} con el N° {v.NumeroFiscal}" +
              (string.IsNullOrWhiteSpace(v.NumeroControl) ? "." : $" y el n° de control {v.NumeroControl}.");
        var pie = new List<string> { emitidoPor };
        if (!emisorEsFiscal)
            pie.Add("SIN VALOR FISCAL: emitido con el simulador del emisor fiscal.");
        pie.Add($"Impreso el {(ahora ?? DateTime.Now):dd/MM/yyyy HH:mm}.");

        return new ComprobanteNoFiscal(
            Valor(organizacion.RazonSocial.Length > 0 ? organizacion.RazonSocial : organizacion.Nombre),
            Valor(organizacion.Rif),
            organizacion.DireccionFiscal,
            organizacion.Telefono,
            titulo,
            TextoBanda,
            datos,
            cliente,
            renglones,
            TextosDeVenta.Totales(v).Select(t => (t.Concepto, t.Monto.ToString("N2"))).ToList(),
            v.Total.ToString("N2"),
            v.TotalUsd > 0 ? $"Referencia: US$ {v.TotalUsd:N2}" : string.Empty,
            pagos,
            pie,
            NombreDeArchivo(v));
    }

    /// <summary>"Factura-F00000012.pdf", "NC-NC00000003.pdf"; sin caracteres que Windows no acepte.</summary>
    private static string NombreDeArchivo(Venta v)
    {
        var numero = string.IsNullOrWhiteSpace(v.NumeroFiscal) ? v.Numero : v.NumeroFiscal;
        var prefijo = v.Tipo switch
        {
            TipoDocumentoVenta.NotaCredito => "NC",
            TipoDocumentoVenta.NotaDebito => "ND",
            _ => "Factura"
        };
        var nombre = $"{prefijo}-{numero}.pdf";
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            nombre = nombre.Replace(c, '-');
        return nombre;
    }

    private static string Valor(string? texto) => string.IsNullOrWhiteSpace(texto) ? "—" : texto.Trim();
}
