using System.Windows;
using System.Windows.Media;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// La escala de la interfaz, aplicada a una ventana cualquiera.
///
/// Existía solo dentro de <c>MainWindow</c>, sobre el <c>Grid</c> raíz, así que alcanzaba al shell
/// y a nada más: a 125 % el menú y las tablas crecían, pero los treinta editores CRUD, el inicio
/// de sesión y el primer arranque —que son <c>Window</c> propias— se quedaban a 100 %. Quien sube
/// la escala lo hace porque no ve bien, y justo los formularios donde se escribe seguían pequeños.
///
/// La escala es lógica, no de DPI: se multiplica con el escalado de Windows en vez de sustituirlo.
/// </summary>
public static class EscalaVentana
{
    /// <summary>
    /// El factor guardado, acotado. Fuera de rango vale 1: un archivo de ajustes corrupto no
    /// puede dejar la ventana a escala 40 y sin forma de volver a Configuración para arreglarlo.
    /// </summary>
    public static double Factor
    {
        get
        {
            var escala = Ajustes.Actual.EscalaInterfaz;
            return escala is >= 0.5 and <= 3 ? escala : 1;
        }
    }

    /// <summary>
    /// Escala el contenido de una ventana secundaria y ajusta su ancho para que quepa.
    ///
    /// El ancho hay que tocarlo a mano porque el editor CRUD llega con uno fijo
    /// (<c>ServicioDialogo</c> lo asigna desde <c>AnchoEditor</c>) y con <c>ResizeMode="NoResize"</c>:
    /// escalando solo el contenido, el formulario crecería dentro de una ventana que no crece y
    /// quedaría cortado, sin manera de arrastrar el borde para recuperarlo.
    ///
    /// Se aplica una vez al abrir, y no se suscribe a cambios: son ventanas modales que viven lo
    /// que dura una edición, y la escala se cambia desde Configuración, que no está abierta a la
    /// vez que una de ellas.
    /// </summary>
    public static void Aplicar(Window ventana)
    {
        var factor = Factor;
        if (factor == 1)
            return;

        if (ventana.Content is FrameworkElement raiz)
            raiz.LayoutTransform = new ScaleTransform(factor, factor);

        if (!double.IsNaN(ventana.Width))
            ventana.Width *= factor;

        if (!double.IsNaN(ventana.Height))
            ventana.Height *= factor;

        if (ventana.MaxWidth is > 0 and < double.PositiveInfinity)
            ventana.MaxWidth *= factor;
    }
}
