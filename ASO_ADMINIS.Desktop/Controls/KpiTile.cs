using System.Windows;
using System.Windows.Controls;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Indicador de una sola cifra: etiqueta, valor, nota al pie y una lectura de si el número está
/// bien o no (<see cref="Estado"/>), que es lo que decide el color.
///
/// Había tres implementaciones distintas del mismo objeto y ninguna compartía código: el
/// <c>ItemsControl</c> del dashboard de módulo, cuatro <c>Border</c> escritos a mano en Producto y
/// una tercera versión en Combustible con el valor a 20 px en vez de 26. Aquí es una sola.
///
/// <see cref="Cargando"/> pinta un esqueleto en lugar del valor. Los indicadores del dashboard
/// consultan la base de datos, y hasta ahora lo hacían en el hilo de interfaz: la ventana se
/// quedaba congelada sin decir por qué.
/// </summary>
public class KpiTile : Control
{
    public static readonly DependencyProperty EtiquetaProperty =
        DependencyProperty.Register(nameof(Etiqueta), typeof(string), typeof(KpiTile),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValorProperty =
        DependencyProperty.Register(nameof(Valor), typeof(string), typeof(KpiTile),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty NotaProperty =
        DependencyProperty.Register(nameof(Nota), typeof(string), typeof(KpiTile),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty EstadoProperty =
        DependencyProperty.Register(nameof(Estado), typeof(EstadoIndicador), typeof(KpiTile),
            new PropertyMetadata(EstadoIndicador.Normal));

    public static readonly DependencyProperty CargandoProperty =
        DependencyProperty.Register(nameof(Cargando), typeof(bool), typeof(KpiTile),
            new PropertyMetadata(false));

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

    public string Nota
    {
        get => (string)GetValue(NotaProperty);
        set => SetValue(NotaProperty, value);
    }

    public EstadoIndicador Estado
    {
        get => (EstadoIndicador)GetValue(EstadoProperty);
        set => SetValue(EstadoProperty, value);
    }

    public bool Cargando
    {
        get => (bool)GetValue(CargandoProperty);
        set => SetValue(CargandoProperty, value);
    }
}
