using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Columna de cifras: alineada a la derecha, con dígitos de ancho fijo y con la cabecera del
/// mismo lado que el valor.
///
/// Las veinticinco columnas numéricas de la aplicación traían el mismo <c>ElementStyle</c>
/// copiado a mano con <c>HorizontalAlignment="Right"</c>, y ese es justo el ajuste que impide
/// leerlas: alineado así, el <c>TextBlock</c> se encoge hasta su contenido, se sale de la celda y
/// el borde lo corta sin elipsis. Lo que alinea un valor dentro de una celda que lo contiene es
/// <c>TextAlignment</c>, que deja el bloque ocupando el ancho completo y por tanto recortando
/// bien (ver <see cref="ColumnaTexto"/>).
///
/// Los dígitos van tabulares porque Segoe UI los dibuja proporcionales por defecto: en una
/// columna de montos el "1" ocupa menos que el "8" y las unidades no caen unas sobre otras, que
/// es lo que permite comparar dos importes de un vistazo sin leerlos.
/// </summary>
public sealed class ColumnaNumero : ColumnaTexto
{
    public ColumnaNumero()
    {
        // Una cabecera a la izquierda sobre cifras a la derecha deja el rótulo y su columna en
        // extremos opuestos. El estilo vive en Componentes.xaml, con el resto de la cromática.
        if (Application.Current?.TryFindResource("CabeceraNumeroStyle") is Style cabecera)
        {
            HeaderStyle = cabecera;
        }
    }

    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        var texto = (TextBlock)base.GenerateElement(cell, dataItem);
        texto.TextAlignment = TextAlignment.Right;
        Typography.SetNumeralAlignment(texto, FontNumeralAlignment.Tabular);
        return texto;
    }
}
