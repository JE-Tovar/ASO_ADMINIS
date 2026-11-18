using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// El comprobante no fiscal para la impresora: un <see cref="FlowDocument"/> tamaño carta con las
/// mismas secciones que el PDF (<see cref="ComprobantePdf"/>). La impresora la elige quien imprime
/// en el diálogo de Windows (incluida "Microsoft Print to PDF").
///
/// Los colores van fijos en negro y grises, no con los del tema: el papel es blanco aunque la
/// aplicación esté en tema oscuro.
/// </summary>
public static class ComprobanteImpreso
{
    private const double Ancho = 8.5 * 96;   // carta, en unidades de WPF (1/96")
    private const double Alto = 11 * 96;
    private const double Margen = 2 / 2.54 * 96;   // 2 cm

    private static readonly Brush Tinta = Brushes.Black;
    private static readonly Brush Gris = new SolidColorBrush(Color.FromRgb(235, 235, 235));
    private static readonly Brush Linea = new SolidColorBrush(Color.FromRgb(160, 160, 160));

    /// <returns><c>true</c> si se mandó a imprimir; <c>false</c> si se canceló el diálogo.</returns>
    public static bool Imprimir(ComprobanteNoFiscal c)
    {
        var dialogo = new PrintDialog();
        if (dialogo.ShowDialog() != true)
            return false;

        var documento = Documento(c);
        dialogo.PrintDocument(((IDocumentPaginatorSource)documento).DocumentPaginator, c.Titulo);
        return true;
    }

    public static FlowDocument Documento(ComprobanteNoFiscal c)
    {
        var d = new FlowDocument
        {
            PageWidth = Ancho,
            PageHeight = Alto,
            PagePadding = new Thickness(Margen),
            ColumnWidth = Ancho,   // una sola columna
            FontFamily = new FontFamily("Arial"),
            FontSize = 12,
            Foreground = Tinta,
            Background = Brushes.White
        };

        d.Blocks.Add(Texto(c.EmisorRazonSocial, 17, negrita: true));
        d.Blocks.Add(Texto($"RIF {c.EmisorRif}", 12));
        if (c.EmisorDireccion.Length > 0) d.Blocks.Add(Texto(c.EmisorDireccion, 12));
        if (c.EmisorTelefono.Length > 0) d.Blocks.Add(Texto($"Teléfono {c.EmisorTelefono}", 12));

        var titulo = Texto(c.Titulo, 20, negrita: true);
        titulo.Margin = new Thickness(0, 16, 0, 0);
        d.Blocks.Add(titulo);

        var banda = Texto(c.Banda, 14, negrita: true);
        banda.TextAlignment = TextAlignment.Center;
        banda.Background = Gris;
        banda.BorderBrush = Linea;
        banda.BorderThickness = new Thickness(1);
        banda.Padding = new Thickness(4);
        banda.Margin = new Thickness(0, 8, 0, 12);
        d.Blocks.Add(banda);

        // Documento y cliente, lado a lado.
        var cabecera = Tabla(1, 0.05, 1);
        var fila = new TableRow();
        fila.Cells.Add(Pares("Documento", c.Datos));
        fila.Cells.Add(new TableCell());
        fila.Cells.Add(Pares("Cliente", c.Cliente));
        cabecera.RowGroups[0].Rows.Add(fila);
        d.Blocks.Add(cabecera);

        // Renglones.
        var renglones = Tabla(1.3, 4.5, 1.4, 0.8, 1.6);
        renglones.Margin = new Thickness(0, 14, 0, 0);
        renglones.FontSize = 11;
        var encabezado = new TableRow { Background = Gris, FontWeight = FontWeights.Bold };
        foreach (var (t, a) in new[] { ("Cantidad", TextAlignment.Left), ("Descripción", TextAlignment.Left),
                                       ("Precio Bs", TextAlignment.Right), ("Alíc.", TextAlignment.Center), ("Monto Bs", TextAlignment.Right) })
            encabezado.Cells.Add(Celda(t, a, borde: true));
        renglones.RowGroups[0].Rows.Add(encabezado);
        foreach (var r in c.Renglones)
        {
            var f = new TableRow();
            f.Cells.Add(Celda(r.Cantidad, TextAlignment.Left, borde: true));
            f.Cells.Add(Celda(r.Descripcion, TextAlignment.Left, borde: true));
            f.Cells.Add(Celda(r.PrecioUnitario, TextAlignment.Right, borde: true));
            f.Cells.Add(Celda(r.Alicuota, TextAlignment.Center, borde: true));
            f.Cells.Add(Celda(r.Monto, TextAlignment.Right, borde: true));
            renglones.RowGroups[0].Rows.Add(f);
        }
        d.Blocks.Add(renglones);

        // Totales.
        // Columna de conceptos ancha: "IGTF 3 % (sobre 1.234.567,89)" tiene que caber en una línea.
        var totales = Tabla(3.4, 3.4, 1.6);
        totales.Margin = new Thickness(0, 10, 0, 0);
        foreach (var (concepto, monto) in c.Totales)
            totales.RowGroups[0].Rows.Add(FilaTotal(concepto, monto, negrita: false));
        totales.RowGroups[0].Rows.Add(FilaTotal("TOTAL Bs", c.Total, negrita: true));
        if (c.TotalUsd.Length > 0)
        {
            var usd = new TableRow();
            usd.Cells.Add(new TableCell());
            var celda = Celda(c.TotalUsd, TextAlignment.Right);
            celda.ColumnSpan = 2;
            usd.Cells.Add(celda);
            totales.RowGroups[0].Rows.Add(usd);
        }
        d.Blocks.Add(totales);

        // Pagos.
        if (c.Pagos.Count > 0)
        {
            var t = Texto("Pagos", 13, negrita: true);
            t.Margin = new Thickness(0, 14, 0, 4);
            d.Blocks.Add(t);
            var pagos = Tabla(2, 2, 3);
            foreach (var (metodo, monto, referencia) in c.Pagos)
            {
                var f = new TableRow();
                f.Cells.Add(Celda(metodo, TextAlignment.Left));
                f.Cells.Add(Celda(monto, TextAlignment.Right));
                f.Cells.Add(Celda(string.IsNullOrWhiteSpace(referencia) ? string.Empty : $"Ref. {referencia}", TextAlignment.Left));
                pagos.RowGroups[0].Rows.Add(f);
            }
            d.Blocks.Add(pagos);
        }

        // Pie.
        var pie = new Paragraph
        {
            Margin = new Thickness(0, 20, 0, 0),
            Padding = new Thickness(0, 6, 0, 0),
            BorderBrush = Linea,
            BorderThickness = new Thickness(0, 1, 0, 0),
            FontSize = 10
        };
        for (var i = 0; i < c.Pie.Count; i++)
        {
            if (i > 0) pie.Inlines.Add(new LineBreak());
            pie.Inlines.Add(new Run(c.Pie[i]));
        }
        d.Blocks.Add(pie);

        return d;
    }

    private static Paragraph Texto(string texto, double tamano, bool negrita = false) => new(new Run(texto))
    {
        FontSize = tamano,
        FontWeight = negrita ? FontWeights.Bold : FontWeights.Normal,
        Margin = new Thickness(0)
    };

    private static Table Tabla(params double[] proporciones)
    {
        var t = new Table { CellSpacing = 0 };
        foreach (var p in proporciones)
            t.Columns.Add(new TableColumn { Width = new GridLength(p, GridUnitType.Star) });
        t.RowGroups.Add(new TableRowGroup());
        return t;
    }

    private static TableCell Celda(string texto, TextAlignment alineacion, bool borde = false) => new(new Paragraph(new Run(texto))
    {
        TextAlignment = alineacion,
        Margin = new Thickness(0)
    })
    {
        Padding = new Thickness(4, 2, 4, 2),
        BorderBrush = borde ? Linea : null,
        BorderThickness = borde ? new Thickness(0.5) : new Thickness(0)
    };

    private static TableCell Pares(string titulo, IReadOnlyList<(string Etiqueta, string Valor)> pares)
    {
        var celda = new TableCell();
        var t = Texto(titulo, 13, negrita: true);
        t.Margin = new Thickness(0, 0, 0, 4);
        celda.Blocks.Add(t);
        foreach (var (etiqueta, valor) in pares)
        {
            var p = new Paragraph { Margin = new Thickness(0) };
            p.Inlines.Add(new Bold(new Run($"{etiqueta}: ")));
            p.Inlines.Add(new Run(valor));
            celda.Blocks.Add(p);
        }
        return celda;
    }

    private static TableRow FilaTotal(string concepto, string monto, bool negrita)
    {
        var f = new TableRow { FontWeight = negrita ? FontWeights.Bold : FontWeights.Normal };
        f.Cells.Add(new TableCell());
        f.Cells.Add(Celda(concepto, TextAlignment.Right));
        var m = Celda(monto, TextAlignment.Right);
        if (negrita)
        {
            m.BorderBrush = Tinta;
            m.BorderThickness = new Thickness(0, 1, 0, 0);
        }
        f.Cells.Add(m);
        return f;
    }
}
