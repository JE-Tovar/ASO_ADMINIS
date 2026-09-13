using System;
using System.Globalization;
using System.Windows.Data;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Texto que muestra la caja cerrada de un <c>ComboBox</c>: resuelve el
/// <c>DisplayMemberPath</c> contra el elemento seleccionado.
///
/// Existe porque la plantilla propia del ComboBox (<c>Styles/Controles.xaml</c>) no puede
/// apoyarse en <c>SelectionBoxItemTemplate</c>: cuando la lista usa <c>DisplayMemberPath</c> esa
/// propiedad se queda en null —se comprobó midiéndola, y también en el ComboBox de fábrica—, y el
/// <c>ContentPresenter</c> acaba pintando el <c>ToString()</c> del objeto. En un <c>record</c> de
/// C# eso es el volcado de todas sus propiedades: "OpcionTema { Valor = Oscuro, Texto = Oscuro }".
///
/// Resolver la ruta aquí deja la plantilla sin depender de mecanismos internos del framework.
/// Las listas que sí traen <c>ItemTemplate</c> no pasan por aquí: la plantilla las pinta con su
/// <c>ContentPresenter</c>.
/// </summary>
public sealed class SeleccionATexto : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [var seleccion, var ruta, ..] || seleccion is null)
            return string.Empty;

        if (ruta is not string camino || string.IsNullOrWhiteSpace(camino))
            return seleccion.ToString() ?? string.Empty;

        var valor = seleccion;

        // Admite rutas anidadas ("Activo.Nombre"), igual que DisplayMemberPath.
        foreach (var tramo in camino.Split('.'))
        {
            if (valor is null)
                return string.Empty;

            var propiedad = valor.GetType().GetProperty(tramo);
            if (propiedad is null)
                return valor.ToString() ?? string.Empty;

            valor = propiedad.GetValue(valor);
        }

        return valor?.ToString() ?? string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("La caja de selección es de solo lectura.");
}
