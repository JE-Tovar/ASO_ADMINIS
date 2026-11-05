using System.Globalization;
using System.IO;
using System.Text;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Libro de Ventas del período (art. 72 y ss. del Reglamento de la Ley de IVA y Providencia
/// 00071, columnas del documento de requisitos más las que faltaban: alícuota adicional,
/// tipo de documento y de transacción, serial/Z y retenciones).
///
/// Reglas de armado:
/// - Las facturas y notas a <b>contribuyentes</b> (cliente con RIF) van una por fila.
/// - Las ventas a <b>consumidor final</b> que ya tienen reporte Z se consolidan en una fila por
///   Z (rango de números del primero al último), como se lleva el libro con máquina fiscal.
///   Sin Z (imprenta digital, o Z aún no hecho) van una por fila, y también las de
///   contingencia, porque llevan su propio número de control de papel.
/// - Las filas van en el orden en que se emitieron los documentos (fecha y hora).
/// - Las notas de crédito restan: sus montos salen en negativo.
/// - Las retenciones se ponen en la fila de su factura si el comprobante es del mismo período;
///   si el comprobante llegó en otro período, salen en una fila propia de ese período.
/// - Los documentos históricos (anteriores al documento fiscal) no entran.
///
/// Las columnas exactas conviene confirmarlas con el contador del cliente.
/// </summary>
public sealed class LibroVentasService(IVentaDataSource ventas, IRetencionIvaDataSource retenciones)
{
    public LibroVentas Generar(DateTime desde, DateTime hasta)
    {
        var d = desde.Date;
        var h = hasta.Date;
        bool EnPeriodo(DateTime f) => f.Date >= d && f.Date <= h;

        var docs = ventas.GetAll()
            .Where(v => v.Vigente && !v.EsHistorico && EnPeriodo(v.Fecha))
            .OrderBy(v => v.Fecha).ThenBy(v => v.Id)
            .ToList();

        var todasRetenciones = retenciones.GetAll().ToList();
        var retencionPorVenta = todasRetenciones.ToDictionary(r => r.VentaId);

        var filas = new List<FilaLibroVentas>();

        // Consumidor final con Z: una fila por (caja, Z). El resto, una fila por documento.
        var consolidables = docs.Where(v => v.EsConsumidorFinal && v.NumeroZ is not null && !v.EnContingencia).ToList();
        var individuales = docs.Except(consolidables);

        foreach (var v in individuales)
        {
            var fila = FilaDe(v);
            if (retencionPorVenta.TryGetValue(v.Id, out var r) && EnPeriodo(r.FechaComprobante))
            {
                fila.IvaRetenido = r.MontoRetenido;
                fila.ComprobanteRetencion = r.NumeroComprobante;
            }
            filas.Add(fila);
        }

        foreach (var grupo in consolidables.GroupBy(v => (v.CajaCodigo, v.NumeroZ)))
        {
            var lista = grupo.OrderBy(v => v.Fecha).ToList();
            var facturas = lista.Where(v => v.Tipo == TipoDocumentoVenta.Factura).ToList();
            decimal S(Func<Venta, decimal> f) => lista.Sum(v => v.Signo * f(v));

            filas.Add(new FilaLibroVentas
            {
                Fecha = lista.Last().Fecha.Date,
                Orden = lista.Last().Fecha,
                RifCliente = string.Empty,
                NombreCliente = $"Resumen diario · consumidor final · Z {grupo.Key.NumeroZ}",
                TipoDocumento = "RZ",
                TipoTransaccion = "01-REG",
                NumeroFactura = facturas.Count switch
                {
                    0 => string.Empty,
                    1 => facturas[0].NumeroFiscal,
                    _ => $"{facturas.First().NumeroFiscal} al {facturas.Last().NumeroFiscal}"
                },
                NumeroControl = string.Empty,
                SerialZ = $"{lista.First().SerialEmisor} / Z {grupo.Key.NumeroZ}",
                TotalConIva = S(v => v.TotalConIva),
                Exento = S(v => v.MontoExento),
                BaseGeneral = S(v => v.BaseGeneral),
                PorcentajeGeneral = lista.FirstOrDefault(v => v.BaseGeneral != 0)?.PorcentajeGeneral ?? 0m,
                IvaGeneral = S(v => v.IvaGeneral),
                BaseReducida = S(v => v.BaseReducida),
                PorcentajeReducida = lista.FirstOrDefault(v => v.BaseReducida != 0)?.PorcentajeReducida ?? 0m,
                IvaReducida = S(v => v.IvaReducida),
                BaseAdicional = S(v => v.BaseAdicional),
                PorcentajeAdicional = lista.FirstOrDefault(v => v.BaseAdicional != 0)?.PorcentajeAdicional ?? 0m,
                IvaAdicional = S(v => v.IvaAdicional),
                Igtf = S(v => v.Igtf)
            });
        }

        // Retenciones que llegaron en este período sobre facturas de períodos anteriores.
        var idsEnLibro = docs.Select(v => v.Id).ToHashSet();
        foreach (var r in todasRetenciones.Where(r => EnPeriodo(r.FechaComprobante) && !idsEnLibro.Contains(r.VentaId)))
        {
            filas.Add(new FilaLibroVentas
            {
                Fecha = r.FechaComprobante,
                Orden = r.FechaComprobante.Date.AddDays(1).AddTicks(-1),
                RifCliente = r.ClienteRif,
                NombreCliente = r.ClienteNombre,
                TipoDocumento = "RET",
                TipoTransaccion = "01-REG",
                NumeroFacturaAfectada = r.VentaNumeroFiscal,
                IvaRetenido = r.MontoRetenido,
                ComprobanteRetencion = r.NumeroComprobante
            });
        }

        filas = [.. filas.OrderBy(f => f.Orden)];
        for (var i = 0; i < filas.Count; i++)
            filas[i].Operacion = i + 1;

        return new LibroVentas(d, h, filas);
    }

    private static FilaLibroVentas FilaDe(Venta v)
    {
        var s = v.Signo;
        var esNota = v.Tipo != TipoDocumentoVenta.Factura;
        return new FilaLibroVentas
        {
            Fecha = v.Fecha.Date,
            Orden = v.Fecha,
            RifCliente = v.ClienteRif,
            NombreCliente = v.EsConsumidorFinal ? "Consumidor final" : v.ClienteNombre,
            TipoDocumento = v.TipoCorto,
            // 01 registro (factura), 02 complemento (nota de débito), 03 anulación/ajuste (nota de crédito).
            TipoTransaccion = v.Tipo switch
            {
                TipoDocumentoVenta.NotaDebito => "02-COMP",
                TipoDocumentoVenta.NotaCredito => "03-ANU",
                _ => "01-REG"
            },
            NumeroFactura = esNota ? string.Empty : v.NumeroFiscal,
            NumeroControl = v.NumeroControl,
            NumeroNota = esNota ? v.NumeroFiscal : string.Empty,
            NumeroFacturaAfectada = v.DocumentoAfectadoNumero,
            SerialZ = v.NumeroZ is { } z ? $"{v.SerialEmisor} / Z {z}" : v.SerialEmisor,
            TotalConIva = s * v.TotalConIva,
            Exento = s * v.MontoExento,
            BaseGeneral = s * v.BaseGeneral,
            PorcentajeGeneral = v.BaseGeneral != 0 ? v.PorcentajeGeneral : 0m,
            IvaGeneral = s * v.IvaGeneral,
            BaseReducida = s * v.BaseReducida,
            PorcentajeReducida = v.BaseReducida != 0 ? v.PorcentajeReducida : 0m,
            IvaReducida = s * v.IvaReducida,
            BaseAdicional = s * v.BaseAdicional,
            PorcentajeAdicional = v.BaseAdicional != 0 ? v.PorcentajeAdicional : 0m,
            IvaAdicional = s * v.IvaAdicional,
            Igtf = s * v.Igtf
        };
    }

    /// <summary>
    /// Exporta el libro a CSV para el contador: separador ";" y decimales con coma, que es lo
    /// que Excel en español abre sin preguntar. UTF-8 con BOM para que respete los acentos.
    /// </summary>
    public static void ExportarCsv(LibroVentas libro, Organizacion emisor, string ruta)
    {
        var es = CultureInfo.GetCultureInfo("es-VE");
        string N(decimal x) => x == 0 ? "0,00" : x.ToString("0.00", es);
        string P(decimal x) => x == 0 ? "" : x.ToString("0.##", es);
        string T(string x) => "\"" + (x ?? string.Empty).Replace("\"", "\"\"") + "\"";

        var sb = new StringBuilder();
        sb.AppendLine($"{T("LIBRO DE VENTAS")}");
        sb.AppendLine($"{T(emisor.RazonSocial)};{T("RIF " + emisor.Rif)}");
        sb.AppendLine($"{T($"Período: {libro.Desde:dd/MM/yyyy} al {libro.Hasta:dd/MM/yyyy}")}");
        sb.AppendLine();
        sb.AppendLine(string.Join(";", FilaLibroVentas.Encabezados.Select(T)));

        foreach (var f in libro.Filas)
        {
            sb.AppendLine(string.Join(";",
            [
                f.Operacion.ToString(), f.Fecha.ToString("dd/MM/yyyy"), T(f.RifCliente), T(f.NombreCliente),
                T(f.TipoDocumento), T(f.TipoTransaccion), T(f.NumeroFactura), T(f.NumeroControl), T(f.NumeroNota),
                T(f.NumeroFacturaAfectada), T(f.SerialZ),
                N(f.TotalConIva), N(f.Exento),
                N(f.BaseGeneral), P(f.PorcentajeGeneral), N(f.IvaGeneral),
                N(f.BaseReducida), P(f.PorcentajeReducida), N(f.IvaReducida),
                N(f.BaseAdicional), P(f.PorcentajeAdicional), N(f.IvaAdicional),
                N(f.Igtf), N(f.IvaRetenido), T(f.ComprobanteRetencion)
            ]));
        }

        var t = libro.Totales;
        sb.AppendLine(string.Join(";",
        [
            "", "", "", T("TOTALES"), "", "", "", "", "", "", "",
            N(t.TotalConIva), N(t.Exento), N(t.BaseGeneral), "", N(t.IvaGeneral),
            N(t.BaseReducida), "", N(t.IvaReducida), N(t.BaseAdicional), "", N(t.IvaAdicional),
            N(t.Igtf), N(t.IvaRetenido), ""
        ]));

        File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }
}

/// <summary>El libro de un período: sus filas numeradas y los totales.</summary>
public sealed record LibroVentas(DateTime Desde, DateTime Hasta, IReadOnlyList<FilaLibroVentas> Filas)
{
    public FilaLibroVentas Totales { get; } = new()
    {
        NombreCliente = "TOTALES",
        TotalConIva = Filas.Sum(f => f.TotalConIva),
        Exento = Filas.Sum(f => f.Exento),
        BaseGeneral = Filas.Sum(f => f.BaseGeneral),
        IvaGeneral = Filas.Sum(f => f.IvaGeneral),
        BaseReducida = Filas.Sum(f => f.BaseReducida),
        IvaReducida = Filas.Sum(f => f.IvaReducida),
        BaseAdicional = Filas.Sum(f => f.BaseAdicional),
        IvaAdicional = Filas.Sum(f => f.IvaAdicional),
        Igtf = Filas.Sum(f => f.Igtf),
        IvaRetenido = Filas.Sum(f => f.IvaRetenido)
    };

    /// <summary>Débito fiscal del período: el IVA de todas las alícuotas.</summary>
    public decimal DebitoFiscal => Totales.IvaGeneral + Totales.IvaReducida + Totales.IvaAdicional;
}

/// <summary>Una fila del libro de ventas.</summary>
public sealed class FilaLibroVentas
{
    public static readonly string[] Encabezados =
    [
        "N° Operación", "Fecha", "RIF / C.I. del cliente", "Nombre o razón social", "Tipo doc.",
        "Tipo transacción", "N° Factura", "N° Control", "N° Nota débito/crédito", "N° Factura afectada",
        "Serial máquina / Z", "Total ventas incluyendo IVA", "Ventas exentas / no sujetas",
        "Base imponible alícuota general", "% Alícuota general", "IVA alícuota general",
        "Base imponible alícuota reducida", "% Alícuota reducida", "IVA alícuota reducida",
        "Base imponible alícuota adicional", "% Alícuota adicional", "IVA alícuota adicional",
        "IGTF percibido", "IVA retenido por el comprador", "N° comprobante de retención"
    ];

    public int Operacion { get; set; }
    public DateTime Fecha { get; set; }

    /// <summary>Momento de emisión, para ordenar las filas; no se exporta.</summary>
    public DateTime Orden { get; set; }
    public string RifCliente { get; set; } = string.Empty;
    public string NombreCliente { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public string TipoTransaccion { get; set; } = string.Empty;
    public string NumeroFactura { get; set; } = string.Empty;
    public string NumeroControl { get; set; } = string.Empty;
    public string NumeroNota { get; set; } = string.Empty;
    public string NumeroFacturaAfectada { get; set; } = string.Empty;
    public string SerialZ { get; set; } = string.Empty;
    public decimal TotalConIva { get; set; }
    public decimal Exento { get; set; }
    public decimal BaseGeneral { get; set; }
    public decimal PorcentajeGeneral { get; set; }
    public decimal IvaGeneral { get; set; }
    public decimal BaseReducida { get; set; }
    public decimal PorcentajeReducida { get; set; }
    public decimal IvaReducida { get; set; }
    public decimal BaseAdicional { get; set; }
    public decimal PorcentajeAdicional { get; set; }
    public decimal IvaAdicional { get; set; }
    public decimal Igtf { get; set; }
    public decimal IvaRetenido { get; set; }
    public string ComprobanteRetencion { get; set; } = string.Empty;

    public string FechaTexto => Fecha == default ? string.Empty : Fecha.ToString("dd/MM/yyyy");
}
