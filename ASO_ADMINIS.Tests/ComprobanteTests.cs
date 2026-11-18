using System.IO;
using System.Windows.Documents;
using ASO_ADMINIS.Desktop.Models;
using ASO_ADMINIS.Desktop.Services;
using PdfSharp.Pdf.IO;

namespace ASO_ADMINIS.Tests;

public class ComprobanteTests
{
    private static Venta FacturaConIgtf() => new()
    {
        Numero = "VTA-000012",
        Tipo = TipoDocumentoVenta.Factura,
        Fecha = new DateTime(2026, 11, 18, 10, 30, 0),
        CajaCodigo = "CAJA-01",
        NumeroFiscal = "F00000012",
        NumeroControl = "00-00000012",
        ClienteRif = "J-00002961-0",
        ClienteNombre = "Ferretería El Tornillo, C.A.",
        ClienteDireccion = "Av. Principal, Caracas",
        TasaCambioUsada = 40m,
        BaseGeneral = 400m, PorcentajeGeneral = 16m, IvaGeneral = 64m,
        BaseIgtf = 464m, PorcentajeIgtf = 3m, Igtf = 13.92m,
        Total = 477.92m,
        Lineas = [new VentaLinea { Codigo = "ACE-1", Descripcion = "Aceite 15W40", Cantidad = 2, PresentacionNombre = "L", Factor = 1,
                                   PrecioUnitario = 200m, Alicuota = TipoAlicuota.General, PorcentajeIva = 16m, Monto = 400m, Iva = 64m }],
        Pagos = [new VentaPago { Metodo = MetodoPago.EfectivoDivisa, MontoBs = 480m, MontoDivisa = 12m }]
    };

    private static ComprobanteNoFiscal De(Venta v, bool fiscal = true) =>
        ComprobanteNoFiscal.De(v, Escenario.Organizacion(), "Máquina de prueba", fiscal, new DateTime(2026, 11, 18, 12, 0, 0));

    [Fact]
    public void Dice_que_no_es_fiscal_y_cual_es_el_documento_fiscal()
    {
        var c = De(FacturaConIgtf());

        Assert.Equal("Factura N° F00000012", c.Titulo);
        Assert.Contains("NO FISCAL", c.Banda);
        Assert.Contains(c.Pie, l => l.Contains("no tiene validez fiscal") && l.Contains("F00000012") && l.Contains("00-00000012"));
        Assert.DoesNotContain(c.Pie, l => l.Contains("simulador"));
    }

    [Fact]
    public void Con_el_simulador_lo_avisa()
    {
        Assert.Contains(De(FacturaConIgtf(), fiscal: false).Pie, l => l.Contains("SIN VALOR FISCAL"));
    }

    [Fact]
    public void Los_totales_son_los_mismos_que_los_de_la_ficha()
    {
        var v = FacturaConIgtf();
        var c = De(v);

        Assert.Equal(TextosDeVenta.Totales(v).Select(t => t.Concepto), c.Totales.Select(t => t.Concepto));
        Assert.Contains(c.Totales, t => t.Concepto.StartsWith("IGTF 3 %"));
        Assert.Equal(477.92m.ToString("N2"), c.Total);
        Assert.Contains("US$", c.TotalUsd);
        Assert.Contains("US$ 12", Assert.Single(c.Pagos).Monto.Replace(",00", "").Replace(".00", ""));
    }

    [Fact]
    public void Una_nota_de_credito_muestra_la_factura_afectada()
    {
        var nota = FacturaConIgtf();
        nota.Tipo = TipoDocumentoVenta.NotaCredito;
        nota.NumeroFiscal = "NC00000003";
        nota.DocumentoAfectadoNumero = "F00000012";
        nota.DocumentoAfectadoFecha = new DateTime(2026, 11, 3);
        nota.Concepto = "Devolución";
        nota.Pagos = [];

        var c = De(nota);

        Assert.Equal("Nota de crédito N° NC00000003", c.Titulo);
        Assert.Contains(c.Datos, d => d.Etiqueta == "Documento afectado" && d.Valor == "Factura N° F00000012 del 03/11/2026");
        Assert.Equal("NC-NC00000003.pdf", c.NombreArchivo);
    }

    [Fact]
    public void A_consumidor_final_lo_dice_en_vez_de_un_rif_vacio()
    {
        var v = FacturaConIgtf();
        v.ClienteRif = "";
        v.ClienteNombre = "";

        var c = De(v);

        Assert.Contains(c.Cliente, d => d.Valor == "Consumidor final");
    }

    [Fact]
    public void El_pdf_es_valido_y_los_renglones_largos_siguen_en_otra_pagina()
    {
        var corta = Paginas(FacturaConIgtf());

        var larga = FacturaConIgtf();
        larga.Lineas = [.. Enumerable.Range(1, 80).Select(i => new VentaLinea
        {
            Codigo = $"ART-{i}", Descripcion = $"Artículo número {i}", Cantidad = 1, Factor = 1,
            PrecioUnitario = 5m, Alicuota = TipoAlicuota.Exento, Monto = 5m
        })];

        Assert.Equal(1, corta);
        Assert.True(Paginas(larga) > 1);
    }

    [Fact]
    public void El_documento_para_imprimir_se_arma_y_pagina()
    {
        var c = De(FacturaConIgtf());
        int? paginas = null;
        Exception? error = null;

        // Los documentos de WPF exigen un hilo STA, que xUnit no da.
        var hilo = new Thread(() =>
        {
            try
            {
                var documento = ComprobanteImpreso.Documento(c);
                var paginador = ((IDocumentPaginatorSource)documento).DocumentPaginator;
                paginador.ComputePageCount();
                paginas = paginador.PageCount;
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        hilo.SetApartmentState(ApartmentState.STA);
        hilo.Start();
        hilo.Join();

        Assert.Null(error);
        Assert.Equal(1, paginas);
    }

    private static int Paginas(Venta v)
    {
        using var flujo = new MemoryStream();
        ComprobantePdf.Escribir(De(v), flujo);

        Assert.True(flujo.Length > 1000);
        flujo.Position = 0;
        var cabecera = new byte[4];
        flujo.ReadExactly(cabecera);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(cabecera));

        flujo.Position = 0;
        using var pdf = PdfReader.Open(flujo, PdfDocumentOpenMode.Import);
        return pdf.PageCount;
    }
}
