using System;
using System.Windows.Input;

namespace ASO_ADMINIS.Desktop.ViewModels;

/// <summary>
/// Comando mínimo para exponer acciones de ViewModel a la UI (Button.Command)
/// con soporte de <c>CanExecute</c>.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}

/// <summary>Variante con parámetro, para listas donde cada elemento invoca la misma acción.</summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;

    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
        => parameter is T valor ? _canExecute?.Invoke(valor) ?? true : false;

    public void Execute(object? parameter)
    {
        if (parameter is T valor)
            _execute(valor);
    }
}
