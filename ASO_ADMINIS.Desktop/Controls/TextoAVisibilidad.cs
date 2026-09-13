using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Oculta un elemento cuando su texto está vacío.
///
/// Los componentes de <c>Styles/Componentes.xaml</c> traen ranuras opcionales —la ayuda de un
/// campo, el error de validación, la nota de un indicador— y sin esto cada una dejaría un hueco
/// en blanco cuando no tiene nada que decir. El proyecto no tenía ningún converter propio: el
/// único en uso era el <c>BooleanToVisibilityConverter</c> del framework.
/// </summary>
public sealed class TextoAVisibilidad : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("La visibilidad derivada del texto es de solo lectura.");
}
