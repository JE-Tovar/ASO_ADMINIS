using System;
using System.Windows.Controls;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.ViewModels;

namespace ASO_ADMINIS.Desktop.Controls;

public partial class Sidebar : UserControl
{
    private readonly SidebarViewModel _viewModel = new();

    /// <summary>
    /// Se dispara cuando el usuario elige un módulo o uno de sus submódulos.
    /// </summary>
    public event EventHandler<NavegacionEventArgs>? NavegacionSolicitada;

    public Sidebar()
    {
        InitializeComponent();

        DataContext = _viewModel;
        _viewModel.NavegacionSolicitada += (_, e) => NavegacionSolicitada?.Invoke(this, e);
    }

    /// <summary>Marca en el menú la sección visible, sin volver a pedir navegación.</summary>
    public void Sincronizar(Modulo modulo, Submodulo? submodulo) => _viewModel.Sincronizar(modulo, submodulo);
}
