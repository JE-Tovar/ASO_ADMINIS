using System;
using System.Collections.Generic;
using System.Windows.Input;
using ASO_ADMINIS.Desktop.Navigation;
using ASO_ADMINIS.Desktop.Services;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>Lanzador: tarjeta por módulo con su lista de submódulos.</summary>
public sealed class InicioViewModel : ViewModelBase
{
    public event EventHandler<Modulo>? ModuloSolicitado;

    public IReadOnlyList<Modulo> Modulos { get; }

    public ICommand AbrirModuloCommand { get; }

    public InicioViewModel() : this(SesionActual.Instancia) { }

    public InicioViewModel(ISesionActual sesion)
    {
        Modulos = sesion.ModulosVisibles();
        AbrirModuloCommand = new RelayCommand<Modulo>(m => ModuloSolicitado?.Invoke(this, m));
    }
}
