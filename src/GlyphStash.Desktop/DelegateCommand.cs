using System.Windows.Input;

namespace GlyphStash.Desktop;

internal sealed class DelegateCommand : ICommand
{
    private readonly Action _execute;

    public DelegateCommand(Action execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
