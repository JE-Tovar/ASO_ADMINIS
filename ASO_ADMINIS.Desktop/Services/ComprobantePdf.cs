using System.IO;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// El comprobante no fiscal como PDF tamaño carta (MigraDoc). Se lo puede mandar al cliente por
/// correo o guardar; si los renglones no caben, sigue en otra página con "Página x de y".
/// </summary>
public static class ComprobantePdf
{
    private const string Fuente = FuentesWindows.Familia;

    public static void Guardar(ComprobanteNoFiscal c, string ruta)
    {
        using var archivo = File.Create(ruta);
        Escribir(c, archivo);
    }

    public static void Escribir(ComprobanteNoFiscal c, Stream destino)
    {
        FuentesWindows.Registrar();

        var documento = new Document();
        documento.Info.Title = c.Titulo;
        documento.Info.Subject = ComprobanteNoFiscal.TextoBanda;
        documento.Info.Author = c.EmisorRazonSocial;

        var estilo = documento.Styles[StyleNames.Normal]!;
        estilo.Font.Name = Fuente;
        estilo.Font.Size = 9;

        var seccion = documento.AddSection();
        seccion.PageSetup = documento.DefaultPageSetup.Clone();
        seccion.PageSetup.PageFormat = PageFormat.Letter;
        seccion.PageSetup.TopMargin = Unit.FromCentimeter(2);
        seccion.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        seccion.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        seccion.PageSetup.RightMargin = Unit.FromCentimeter(2);

        var pie = seccion.Footers.Primary.AddParagraph();
        pie.Format.Alignment = ParagraphAlignment.Right;
        pie.Format.Font.Size = 7;
        pie.AddText($"{c.Titulo} · {ComprobanteNoFiscal.TextoBanda} · Página ");
        pie.AddPageField();
        pie.AddText(" de ");
        pie.AddNumPagesField();

        // ---- Emisor ----
        Parrafo(seccion, c.EmisorRazonSocial, 13, negrita: true);
        Parrafo(seccion, $"RIF {c.EmisorRif}", 9);
        if (c.EmisorDireccion.Length > 0) Parrafo(seccion, c.EmisorDireccion, 9);
        if (c.EmisorTelefono.Length > 0) Parrafo(seccion, $"Teléfono {c.EmisorTelefono}", 9);

        // ---- Título y banda ----
        var titulo = Parrafo(seccion, c.Titulo, 15, negrita: true);
        titulo.Format.SpaceBefore = Unit.FromPoint(12);

        var banda = Parrafo(seccion, c.Banda, 11, negrita: true);
        banda.Format.Alignment = ParagraphAlignment.Center;
        banda.Format.Shading.Color = new Color(235, 235, 235);
        banda.Format.Borders.Width = Unit.FromPoint(0.75);
        banda.Format.Borders.Color = new Color(120, 120, 120);
        banda.Format.SpaceBefore = Unit.FromPoint(6);
        banda.Format.SpaceAfter = Unit.FromPoint(10);

        // ---- Documento y cliente, lado a lado ----
        var cabecera = seccion.AddTable();
        cabecera.AddColumn(Unit.FromCentimeter(8.6));
        cabecera.AddColumn(Unit.FromCentimeter(0.4));
        cabecera.AddColumn(Unit.FromCentimeter(8.6));
        var fila = cabecera.AddRow();
        Pares(fila.Cells[0], "Documento", c.Datos);
        Pares(fila.Cells[2], "Cliente", c.Cliente);

        // ---- Renglones ----
        var espacio = seccion.AddParagraph();
        espacio.Format.SpaceBefore = Unit.FromPoint(10);

        var tabla = seccion.AddTable();
        tabla.Borders.Color = new Color(190, 190, 190);
        tabla.Borders.Width = Unit.FromPoint(0.5);
        tabla.Format.Font.Size = 8.5;
        tabla.AddColumn(Unit.FromCentimeter(2.4));
        tabla.AddColumn(Unit.FromCentimeter(8.2)).Format.Alignment = ParagraphAlignment.Left;
        tabla.AddColumn(Unit.FromCentimeter(2.5)).Format.Alignment = ParagraphAlignment.Right;
        tabla.AddColumn(Unit.FromCentimeter(1.6)).Format.Alignment = ParagraphAlignment.Center;
        tabla.AddColumn(Unit.FromCentimeter(2.9)).Format.Alignment = ParagraphAlignment.Right;

        var encabezado = tabla.AddRow();
        encabezado.HeadingFormat = true;   // se repite en cada página
        encabezado.Format.Font.Bold = true;
        encabezado.Shading.Color = new Color(235, 235, 235);
        string[] titulos = ["Cantidad", "Descripción", "Precio Bs", "Alíc.", "Monto Bs"];
        for (var i = 0; i < titulos.Length; i++)
            encabezado.Cells[i].AddParagraph(titulos[i]);

        foreach (var r in c.Renglones)
        {
            var f = tabla.AddRow();
            f.Cells[0].AddParagraph(r.Cantidad);
            f.Cells[1].AddParagraph(r.Descripcion);
            f.Cells[2].AddParagraph(r.PrecioUnitario);
            f.Cells[3].AddParagraph(r.Alicuota);
            f.Cells[4].AddParagraph(r.Monto);
        }

        // ---- Totales, a la derecha ----
        var totales = seccion.AddTable();
        totales.Format.Font.Size = 9;
        // Columna de conceptos ancha: "IGTF 3 % (sobre 1.234.567,89)" tiene que caber en una línea.
        totales.AddColumn(Unit.FromCentimeter(7.7));
        totales.AddColumn(Unit.FromCentimeter(7.0)).Format.Alignment = ParagraphAlignment.Right;
        totales.AddColumn(Unit.FromCentimeter(2.9)).Format.Alignment = ParagraphAlignment.Right;
        var primera = totales.AddRow();
        primera.TopPadding = Unit.FromPoint(8);
        foreach (var (concepto, monto) in c.Totales)
            FilaTotal(totales, concepto, monto, negrita: false);
        FilaTotal(totales, "TOTAL Bs", c.Total, negrita: true);
        if (c.TotalUsd.Length > 0)
            FilaTotal(totales, string.Empty, c.TotalUsd, negrita: false, combinar: true);

        // ---- Pagos ----
        if (c.Pagos.Count > 0)
        {
            var t = Parrafo(seccion, "Pagos", 10, negrita: true);
            t.Format.SpaceBefore = Unit.FromPoint(12);
            var pagos = seccion.AddTable();
            pagos.AddColumn(Unit.FromCentimeter(5));
            pagos.AddColumn(Unit.FromCentimeter(5)).Format.Alignment = ParagraphAlignment.Right;
            pagos.AddColumn(Unit.FromCentimeter(7.6));
            foreach (var (metodo, monto, referencia) in c.Pagos)
            {
                var f = pagos.AddRow();
                f.Cells[0].AddParagraph(metodo);
                f.Cells[1].AddParagraph(monto);
                f.Cells[2].AddParagraph(string.IsNullOrWhiteSpace(referencia) ? string.Empty : $"Ref. {referencia}");
            }
        }

        // ---- Pie ----
        var aviso = seccion.AddParagraph();
        aviso.Format.SpaceBefore = Unit.FromPoint(18);
        aviso.Format.Borders.Top.Width = Unit.FromPoint(0.5);
        aviso.Format.Borders.Top.Color = new Color(150, 150, 150);
        aviso.Format.Font.Size = 8;
        foreach (var linea in c.Pie)
        {
            aviso.AddText(linea);
            aviso.AddLineBreak();
        }

        var renderer = new PdfDocumentRenderer { Document = documento };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(destino, closeStream: false);
    }

    private static Paragraph Parrafo(Section seccion, string texto, double tamano, bool negrita = false)
    {
        var p = seccion.AddParagraph(texto);
        p.Format.Font.Size = tamano;
        p.Format.Font.Bold = negrita;
        return p;
    }

    private static void Pares(Cell celda, string titulo, IReadOnlyList<(string Etiqueta, string Valor)> pares)
    {
        var t = celda.AddParagraph(titulo);
        t.Format.Font.Bold = true;
        t.Format.Font.Size = 10;
        t.Format.SpaceAfter = Unit.FromPoint(3);
        foreach (var (etiqueta, valor) in pares)
        {
            var p = celda.AddParagraph();
            p.AddFormattedText($"{etiqueta}: ", TextFormat.Bold);
            p.AddText(valor);
        }
    }

    private static void FilaTotal(Table tabla, string concepto, string monto, bool negrita, bool combinar = false)
    {
        var f = tabla.AddRow();
        f.Format.Font.Bold = negrita;
        if (combinar)
        {
            f.Cells[1].MergeRight = 1;
            f.Cells[1].AddParagraph(monto);
            return;
        }
        f.Cells[1].AddParagraph(concepto);
        f.Cells[2].AddParagraph(monto);
        if (negrita)
            f.Cells[2].Borders.Top.Width = Unit.FromPoint(0.75);
    }
}

/// <summary>
/// PDFsharp 6 no encuentra solo las fuentes de Windows: hay que decirle dónde están. Se usa Arial
/// (viene con Windows); si faltara, Segoe UI. Se registra una sola vez por proceso.
/// </summary>
internal sealed class FuentesWindows : IFontResolver
{
    public const string Familia = "Arial";

    private static readonly object Cerrojo = new();
    private static bool _registrado;

    private static readonly string Carpeta = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    private static readonly (string Cara, string[] Archivos)[] Caras =
    [
        ("Arial#R", ["arial.ttf", "segoeui.ttf"]),
        ("Arial#B", ["arialbd.ttf", "segoeuib.ttf"]),
        ("Arial#I", ["ariali.ttf", "segoeuii.ttf"]),
        ("Arial#BI", ["arialbi.ttf", "segoeuiz.ttf"])
    ];

    public static void Registrar()
    {
        lock (Cerrojo)
        {
            if (_registrado || GlobalFontSettings.FontResolver is not null)
            {
                _registrado = true;
                return;
            }

            GlobalFontSettings.FontResolver = new FuentesWindows();
            _registrado = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        // Cualquier familia que pida MigraDoc se resuelve a la nuestra.
        var cara = (bold, italic) switch
        {
            (true, true) => "Arial#BI",
            (true, false) => "Arial#B",
            (false, true) => "Arial#I",
            _ => "Arial#R"
        };
        return new FontResolverInfo(cara);
    }

    public byte[]? GetFont(string faceName)
    {
        var archivos = Caras.FirstOrDefault(c => c.Cara == faceName).Archivos ?? Caras[0].Archivos;
        foreach (var archivo in archivos)
        {
            var ruta = Path.Combine(Carpeta, archivo);
            if (File.Exists(ruta))
                return File.ReadAllBytes(ruta);
        }

        throw new InvalidOperationException($"No se encontró la fuente {string.Join(" ni ", archivos)} en {Carpeta}.");
    }
}
