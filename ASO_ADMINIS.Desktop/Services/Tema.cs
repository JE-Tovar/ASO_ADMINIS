using System;
using System.Windows;
using ASO_ADMINIS.Desktop.Models;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Cambia la paleta de la aplicación en caliente, superponiendo un diccionario.
///
/// El primer intento fue reasignar el <c>Color</c> de cada brush de <c>Colors.xaml</c>, y no
/// funciona: <b>WPF congela los Freezable de un ResourceDictionary compilado al cargarlo</b>, así
/// que ya en el arranque <c>IsFrozen</c> es <c>true</c> y cualquier mutación lanza. Es la razón
/// de que todas las referencias a brushes del XAML sean <c>DynamicResource</c> y no
/// <c>StaticResource</c>: <c>StaticResource</c> resuelve una vez y se queda con el objeto, así
/// que no se enteraría de que se superpuso otra paleta.
///
/// <b>Al escribir XAML nuevo, un color va siempre por <c>DynamicResource</c> a una clave de la
/// paleta.</b> Un <c>StaticResource</c> o un hex escrito a mano se quedan claros sobre el tema
/// oscuro, y el fallo no se ve hasta que alguien cambia de tema.
/// </summary>
public static class Tema
{
    private static readonly Uri _fuenteOscura =
        new("pack://application:,,,/ASO_ADMINIS.Desktop;component/Styles/ColorsOscuro.xaml");

    /// <summary>La paleta oscura mientras está puesta; null con el tema claro.</summary>
    private static ResourceDictionary? _oscuro;

    public static void Aplicar(TemaApp tema)
    {
        var diccionarios = Application.Current?.Resources.MergedDictionaries;
        if (diccionarios is null)
            return;

        if (tema == TemaApp.Oscuro)
        {
            if (_oscuro is not null)
                return;

            // Al final de la lista a propósito: al buscar una clave, los diccionarios fusionados
            // se recorren en orden inverso, así que el último puesto es el primero en responder
            // y es el que tapa a Colors.xaml.
            _oscuro = new ResourceDictionary { Source = _fuenteOscura };
            diccionarios.Add(_oscuro);
            return;
        }

        if (_oscuro is null)
            return;

        diccionarios.Remove(_oscuro);
        _oscuro = null;
    }
}
