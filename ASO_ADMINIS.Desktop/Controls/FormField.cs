using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Un campo de formulario completo: etiqueta, el control de captura (el <c>Content</c>), su texto
/// de ayuda y su error de validación.
///
/// El par etiqueta + campo aparecía unas doscientas veces como dos elementos hermanos sueltos
/// dentro de un <c>StackPanel</c>, sin nada que los uniera. De ahí salían tres problemas a la vez:
/// el margen entre etiqueta y campo lo ponía cada pantalla por su cuenta, el texto de ayuda se
/// repetía a mano con la misma tripleta de atributos y cuatro márgenes distintos, y la validación
/// no tenía dónde vivir —el error era un único <c>TextBlock</c> rojo al pie de la ventana del
/// editor, que solo aparecía al pulsar Guardar y no decía qué campo lo había provocado.
///
/// Resuelve además la asociación etiqueta-campo para lectores de pantalla. Las etiquetas eran
/// <c>TextBlock</c>, no <c>Label</c>, así que no había ninguna: quien navegue con el teclado o con
/// un lector oía el contenido del campo sin saber de qué campo se trataba.
/// </summary>
public class FormField : ContentControl
{
    public static readonly DependencyProperty EtiquetaProperty =
        DependencyProperty.Register(nameof(Etiqueta), typeof(string), typeof(FormField),
            new PropertyMetadata(string.Empty, AlCambiarEtiqueta));

    public static readonly DependencyProperty AyudaProperty =
        DependencyProperty.Register(nameof(Ayuda), typeof(string), typeof(FormField),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ErrorProperty =
        DependencyProperty.Register(nameof(Error), typeof(string), typeof(FormField),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RequeridoProperty =
        DependencyProperty.Register(nameof(Requerido), typeof(bool), typeof(FormField),
            new PropertyMetadata(false));

    public string Etiqueta
    {
        get => (string)GetValue(EtiquetaProperty);
        set => SetValue(EtiquetaProperty, value);
    }

    public string Ayuda
    {
        get => (string)GetValue(AyudaProperty);
        set => SetValue(AyudaProperty, value);
    }

    /// <summary>Vacío mientras el campo sea válido; el mensaje lo pinta en rojo bajo el control.</summary>
    public string Error
    {
        get => (string)GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public bool Requerido
    {
        get => (bool)GetValue(RequeridoProperty);
        set => SetValue(RequeridoProperty, value);
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        Nombrar();
    }

    private static void AlCambiarEtiqueta(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FormField)d).Nombrar();

    /// <summary>
    /// Le da al control de captura el nombre accesible que la etiqueta ya muestra en pantalla.
    /// Se hace aquí y no con <c>Label.Target</c> porque el contenido llega por plantilla y no hay
    /// un <c>ElementName</c> al que apuntar desde el XAML.
    /// </summary>
    private void Nombrar()
    {
        if (Content is DependencyObject destino && !string.IsNullOrWhiteSpace(Etiqueta))
            AutomationProperties.SetName(destino, Etiqueta);
    }
}
