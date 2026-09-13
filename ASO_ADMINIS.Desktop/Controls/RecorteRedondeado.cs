using System.Windows;
using System.Windows.Media;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Recorta un elemento a un rectángulo de esquinas redondeadas, para que quepa dentro de una
/// tarjeta sin desbordarle las curvas.
///
/// Hace falta porque <b>un Border de WPF NO recorta a sus hijos</b>: dibuja su marco con el
/// CornerRadius que le pidas y luego el hijo se pinta encima, cuadrado. En las veinte pantallas
/// de listado la tabla va dentro de una tarjeta (<c>SectionCardStyle</c>, radio RadMd) con
/// <c>Padding="0"</c>, así que la cabecera del DataGrid —que pinta un rectángulo sólido con
/// FilaAlternaBrush— tapaba las dos esquinas de arriba y las filas las de abajo: la tarjeta se
/// veía cuadrada aunque su marco fuera redondo.
///
/// El recorte se rehace en cada <c>SizeChanged</c> porque la geometría lleva el tamaño dentro y
/// no se reevalúa sola: fijarla una vez dejaría media tabla cortada en cuanto cambiara el ancho
/// de la ventana o la escala de la interfaz.
///
/// El radio que se pasa es el INTERIOR: el de la tarjeta menos su grosor de borde (6 - 1 = 5),
/// que es donde empieza de verdad el hueco del contenido.
/// </summary>
public static class RecorteRedondeado
{
    public static readonly DependencyProperty RadioProperty =
        DependencyProperty.RegisterAttached(
            "Radio", typeof(double), typeof(RecorteRedondeado),
            new PropertyMetadata(0d, AlCambiarRadio));

    public static double GetRadio(DependencyObject objeto) => (double)objeto.GetValue(RadioProperty);

    public static void SetRadio(DependencyObject objeto, double valor) =>
        objeto.SetValue(RadioProperty, valor);

    private static void AlCambiarRadio(DependencyObject objeto, DependencyPropertyChangedEventArgs e)
    {
        if (objeto is not FrameworkElement elemento)
            return;

        // Siempre se quita antes de poner: el estilo puede reasignar el radio sobre un elemento
        // reciclado (las filas de un DataGrid virtualizado lo son) y se acumularían suscripciones.
        elemento.SizeChanged -= AlCambiarTamano;

        if ((double)e.NewValue > 0)
        {
            elemento.SizeChanged += AlCambiarTamano;
            Aplicar(elemento);
        }
        else
        {
            elemento.Clip = null;
        }
    }

    private static void AlCambiarTamano(object remitente, SizeChangedEventArgs e) =>
        Aplicar((FrameworkElement)remitente);

    private static void Aplicar(FrameworkElement elemento)
    {
        var radio = GetRadio(elemento);

        // Antes de la primera medida el tamaño es 0 y la geometría saldría vacía, que se veria
        // como una tabla en blanco. Se deja sin recorte y lo pone el SizeChanged que viene detras.
        if (radio <= 0 || elemento.ActualWidth <= 0 || elemento.ActualHeight <= 0)
        {
            elemento.Clip = null;
            return;
        }

        elemento.Clip = new RectangleGeometry(
            new Rect(0, 0, elemento.ActualWidth, elemento.ActualHeight), radio, radio);
    }
}
