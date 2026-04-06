using System.Windows.Input;

namespace Warehouse.Wpf.Infrastructure;

/// <summary>
/// Async command implementation for WPF command bindings.
/// </summary>
public sealed class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private readonly Func<Task> _execute = execute;
    private readonly Func<bool>? _canExecute = canExecute;
    private bool _isExecuting;

    /// <summary>
    /// Occurs when command availability changes.
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Determines whether command execution is currently allowed.
    /// </summary>
    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke() ?? true);
    }

    /// <summary>
    /// Executes the async command operation.
    /// </summary>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Triggers command re-evaluation in the UI.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
