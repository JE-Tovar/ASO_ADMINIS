using System;
using System.Windows;
using ASO_ADMINIS.Desktop.ViewModels;
using ASO_ADMINIS.Desktop.Views;

namespace ASO_ADMINIS.Desktop.Services;

/// <summary>
/// Implementación por defecto de <see cref="IServicioDialogo"/> usando ventanas WPF.
/// </summary>
public class ServicioDialogo : IServicioDialogo
{
    public bool MostrarEditor(CrudEditorViewModelBase editor)
    {
        // El ancho se fija aquí y no por binding: el DataContext se asigna después de que el
        // XAML se parsea, y un formulario largo (la remesa) necesita más de los 420 por defecto.
        var ventana = new CrudEditorWindow { DataContext = editor, Width = editor.AnchoEditor };

        if (Application.Current?.MainWindow is { } owner && owner != ventana)
            ventana.Owner = owner;

        editor.SolicitarCierre += (_, guardo) => ventana.DialogResult = guardo;

        // La escala de la interfaz no llegaba aqui: el LayoutTransform vivia solo en MainWindow,
        // asi que a 125 % el shell crecia y los formularios se quedaban a 100 %.
        EscalaVentana.Aplicar(ventana);

        // Tope contra el área de trabajo de la pantalla, después de escalar: un editor ancho
        // (960 con AnchoEditor.Amplio) a 150 % de escala pasaría de 1400 px, más ancho que muchas
        // pantallas; y un formulario largo con SizeToContent="Height" y sin este tope crecería sin
        // límite, con ResizeMode="NoResize" no dejaría manera de recuperarlo. Width se recorta
        // directo porque ya está fijado; Height se deja que lo siga decidiendo SizeToContent, así
        // que MaxHeight es lo único que hace falta para que el ScrollViewer de la ventana entre a
        // trabajar en vez de salirse de la pantalla.
        var areaTrabajo = SystemParameters.WorkArea;
        if (editor.AbreMaximizado)
        {
            // Mostrador: maximizada y redimensionable. Width/Height son el tamaño al restaurar;
            // los mínimos evitan que, restaurada, el panel de cobro deje de caber.
            ventana.SizeToContent = SizeToContent.Manual;
            ventana.ResizeMode = ResizeMode.CanResize;
            ventana.MinWidth = Math.Min(1000, areaTrabajo.Width);
            ventana.MinHeight = Math.Min(640, areaTrabajo.Height);
            ventana.Width = Math.Max(ventana.MinWidth, areaTrabajo.Width * 0.9);
            ventana.Height = Math.Max(ventana.MinHeight, areaTrabajo.Height * 0.9);
            ventana.WindowState = WindowState.Maximized;
        }
        else
        {
            ventana.Width = Math.Min(ventana.Width, areaTrabajo.Width * 0.9);
            ventana.MaxHeight = areaTrabajo.Height * 0.9;
        }

        return ventana.ShowDialog() == true;
    }

    public bool Confirmar(string titulo, string mensaje)
    {
        var resultado = MessageBox.Show(mensaje, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return resultado == MessageBoxResult.Yes;
    }

    public void Informar(string titulo, string mensaje)
    {
        MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
