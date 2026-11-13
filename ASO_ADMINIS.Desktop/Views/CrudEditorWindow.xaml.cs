using System.Windows;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Desktop.Views;

public partial class CrudEditorWindow : Window
{
    public CrudEditorWindow()
    {
        InitializeComponent();
        KeyDown += AlTeclear;
    }

    /// <summary>
    /// Esc cierra también las fichas de solo lectura. En un editor lo atiende el botón Cancelar
    /// (<c>IsCancel</c>), pero la ficha lo oculta (<c>MuestraCancelar</c> en false) y un botón
    /// colapsado no recibe la tecla. Va en KeyDown y no en PreviewKeyDown para que un control
    /// que use Esc antes (una lista desplegable abierta) lo siga recibiendo primero.
    /// </summary>
    private void AlTeclear(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not CrudEditorViewModelBase { MuestraCancelar: false } ficha)
            return;

        ficha.CancelarCommand.Execute(null);
        e.Handled = true;
    }
}
