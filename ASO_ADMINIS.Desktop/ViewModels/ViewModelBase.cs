using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Base para ViewModels con notificación de cambios (INotifyPropertyChanged).
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Notifica que cambiaron todas las propiedades. Es la convencion de WPF: un
    /// PropertyChanged con nombre vacio hace que se reevaluen todos los bindings del
    /// ViewModel. Para cuando un solo cambio arrastra a varias propiedades derivadas y
    /// enumerarlas a mano seria una lista que se queda corta con el tiempo.
    /// </summary>
    protected void OnTodasLasPropiedadesCambiaron()
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
