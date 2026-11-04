using System.Windows.Input;

namespace Tpos.Desktop.Mvvm;

public sealed class RelayCommand<T>(Action<T?> execute) : ICommand
{
    private readonly Action<T?> _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        if (parameter is null || parameter is T)
        {
            _execute((T?)parameter);
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
