using System.Windows;
using System.Windows.Controls;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Un dato de una ficha de solo lectura: la etiqueta pequeña arriba y el valor debajo.
/// <c>&lt;controls:Dato Etiqueta="Proveedor" Valor="{Binding Documento.ProveedorNombre}" /&gt;</c>
///
/// Si el valor llega vacío el dato entero se oculta: así una ficha puede listar los campos
/// opcionales (observaciones, motivo de anulación, referencia) sin dejar etiquetas huérfanas.
/// Es el equivalente de lectura de <see cref="FormField"/>.
/// </summary>
public class Dato : StackPanel
{
    public static readonly DependencyProperty EtiquetaProperty =
        DependencyProperty.Register(nameof(Etiqueta), typeof(string), typeof(Dato),
            new PropertyMetadata(string.Empty, (d, e) => ((Dato)d)._etiqueta.Text = (string?)e.NewValue ?? string.Empty));

    public static readonly DependencyProperty ValorProperty =
        DependencyProperty.Register(nameof(Valor), typeof(string), typeof(Dato),
            new PropertyMetadata(string.Empty, (d, _) => ((Dato)d).Pintar()));

    private readonly TextBlock _etiqueta;
    private readonly TextBlock _valor;

    public Dato()
    {
        _etiqueta = new TextBlock();
        _etiqueta.SetResourceReference(StyleProperty, "FormLabelStyle");

        _valor = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _valor.SetResourceReference(TextBlock.FontSizeProperty, "FsBody");
        _valor.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

        Children.Add(_etiqueta);
        Children.Add(_valor);
        Pintar();
    }

    public string Etiqueta
    {
        get => (string)GetValue(EtiquetaProperty);
        set => SetValue(EtiquetaProperty, value);
    }

    public string Valor
    {
        get => (string)GetValue(ValorProperty);
        set => SetValue(ValorProperty, value);
    }

    private void Pintar()
    {
        _valor.Text = Valor ?? string.Empty;
        Visibility = string.IsNullOrWhiteSpace(Valor) ? Visibility.Collapsed : Visibility.Visible;
    }
}
