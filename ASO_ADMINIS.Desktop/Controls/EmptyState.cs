using System.Windows;
using System.Windows.Controls;

namespace ASO_ADMINIS.Desktop.Controls;

/// <summary>
/// Lo que se ve donde no hay nada: un glifo, una frase que explica por qué está vacío y, en el
/// <c>Content</c>, la acción que lo llenaría.
///
/// No existía en ninguna forma. Las diecinueve tablas de la aplicación, sin filas, se veían como
/// un encabezado de columnas y espacio en blanco debajo, sin distinguir "no hay datos todavía" de
/// "el filtro no encontró nada" ni de "esto no cargó".
/// </summary>
public class EmptyState : ContentControl
{
    public static readonly DependencyProperty IconoProperty =
        DependencyProperty.Register(nameof(Icono), typeof(string), typeof(EmptyState),
            new PropertyMetadata(Iconos.Buscar));

    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(EmptyState),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DetalleProperty =
        DependencyProperty.Register(nameof(Detalle), typeof(string), typeof(EmptyState),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CompactoProperty =
        DependencyProperty.Register(nameof(Compacto), typeof(bool), typeof(EmptyState),
            new PropertyMetadata(false));

    /// <summary>
    /// Versión reducida, para fichas y ventanas modales. El bloque de página —glifo de 28 y
    /// título de subtítulo— se dimensionó para una tabla que ocupa la pantalla; dentro de un
    /// modal, donde el hueco vacío mide poco más que el propio bloque, sale desproporcionado.
    /// </summary>
    public bool Compacto
    {
        get => (bool)GetValue(CompactoProperty);
        set => SetValue(CompactoProperty, value);
    }

    /// <summary>Glifo de <see cref="Iconos"/>. Por defecto, la lupa.</summary>
    public string Icono
    {
        get => (string)GetValue(IconoProperty);
        set => SetValue(IconoProperty, value);
    }

    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public string Detalle
    {
        get => (string)GetValue(DetalleProperty);
        set => SetValue(DetalleProperty, value);
    }
}
