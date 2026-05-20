using System.Windows.Input;
using IPLab.Core.Interfaces;

namespace IPLab.ViewModels;

public class ToolboxItemViewModel
{
    public string   TypeName   { get; }
    public string   Icon       { get; }
    public ICommand AddCommand { get; }

    public ToolboxItemViewModel(IOperatorType type, Action<IOperatorType> addAction)
    {
        TypeName   = type.TypeName;
        Icon       = type.Icon;
        AddCommand = new RelayCommand(() => addAction(type));
    }
}
