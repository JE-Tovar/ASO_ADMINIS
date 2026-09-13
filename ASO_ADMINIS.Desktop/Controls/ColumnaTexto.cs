using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Columna de texto que, cuando el valor no cabe, lo dice.
///
/// El <c>DataGridTextColumn</c> de fábrica genera un <c>TextBlock</c> sin
/// <c>TextTrimming</c>: lo que sobra del ancho de la celda se corta a media letra, sin puntos
/// suspensivos y sin forma de recuperarlo. En las diecinueve tablas de la aplicación no había ni
/// una sola celda con recorte ni con tooltip, y como los anchos fijos se pasan del área útil en
/// la ventana mínima (1024 px), las columnas <c>Width="*"</c> se quedaban en unas decenas de
/// píxeles: "Lote · Tablón" se leía como dos letras sueltas.
///
/// Aquí el valor se recorta con elipsis —así se ve que hay más— y el tooltip lo devuelve entero.
/// El tooltip se cancela cuando el texto sí cabe: en una tabla densa, una etiqueta flotante sobre
/// cada celda estorba más de lo que ayuda.
/// </summary>
public class ColumnaTexto : DataGridTextColumn
{
    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        var texto = (TextBlock)base.GenerateElement(cell, dataItem);
        Preparar(texto);
        return texto;
    }

    /// <summary>
    /// Deja el <c>TextBlock</c> de una celda recortando con elipsis y con el valor completo en el
    /// tooltip. Lo usa también <see cref="ColumnaNumero"/>.
    /// </summary>
    private protected static void Preparar(TextBlock texto)
    {
        texto.TextTrimming = TextTrimming.CharacterEllipsis;

        // El tooltip es el propio valor de la celda. Se enlaza en vez de copiarse porque la
        // virtualización del DataGrid reutiliza el TextBlock para otras filas al desplazarse.
        texto.SetBinding(FrameworkElement.ToolTipProperty,
                         new Binding(nameof(TextBlock.Text)) { Source = texto });

        texto.ToolTipOpening += CancelarSiCabe;
    }

    private static void CancelarSiCabe(object sender, ToolTipEventArgs e)
    {
        if (sender is TextBlock texto && !EstaRecortado(texto))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Mide el texto contra el ancho que le deja su celda.
    ///
    /// Se compara contra la celda y no contra el <c>ActualWidth</c> del <c>TextBlock</c> porque
    /// son distintos en cuanto el texto se alinea: un bloque alineado a la derecha se ajusta a su
    /// contenido y su <c>ActualWidth</c> nunca delataría el recorte.
    ///
    /// Se calcula al abrirse el tooltip y no en cada cambio de tamaño: así no hay que suscribirse
    /// a nada por celda y el coste sólo se paga al señalar una.
    /// </summary>
    private static bool EstaRecortado(TextBlock texto)
    {
        if (string.IsNullOrEmpty(texto.Text))
        {
            return false;
        }

        var disponible = texto.ActualWidth;
        if (BuscarCelda(texto) is { } celda)
        {
            disponible = celda.ActualWidth - celda.Padding.Left - celda.Padding.Right
                         - celda.BorderThickness.Left - celda.BorderThickness.Right;
        }

        var medida = new FormattedText(
            texto.Text,
            CultureInfo.CurrentUICulture,
            texto.FlowDirection,
            new Typeface(texto.FontFamily, texto.FontStyle, texto.FontWeight, texto.FontStretch),
            texto.FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(texto).PixelsPerDip);

        // Medio píxel de margen: el redondeo del layout no debe contar como recorte.
        return medida.Width > disponible + 0.5;
    }

    private static DataGridCell? BuscarCelda(DependencyObject desde)
    {
        for (var actual = VisualTreeHelper.GetParent(desde);
             actual is not null;
             actual = VisualTreeHelper.GetParent(actual))
        {
            if (actual is DataGridCell celda)
            {
                return celda;
            }
        }

        return null;
    }
}
