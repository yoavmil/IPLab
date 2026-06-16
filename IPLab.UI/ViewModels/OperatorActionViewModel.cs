using System.Windows.Input;

namespace IPLab.UI.ViewModels;

public sealed class OperatorActionViewModel(string label, Action execute, Func<bool>? canExecute = null)
{
    public string Label { get; } = label;
    public ICommand Command { get; } = new RelayCommand(execute, canExecute);
}
