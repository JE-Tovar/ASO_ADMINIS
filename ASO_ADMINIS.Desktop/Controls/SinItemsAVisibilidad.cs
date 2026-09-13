using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Muestra algo justo cuando una lista está vacía: convierte <c>HasItems == false</c> en
/// <c>Visible</c>.
///
/// Es lo que deja poner un <see cref="EmptyState"/> encima de una tabla sin duplicar en el
/// ViewModel una propiedad que el propio <c>DataGrid</c> ya expone.
/// </summary>
public sealed class SinItemsAVisibilidad : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("La visibilidad del estado vacío es de solo lectura.");
}
