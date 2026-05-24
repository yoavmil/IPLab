using System.Collections.ObjectModel;
using IPLab.Core.Interfaces;
using IPLab.Core.Operators;

namespace IPLab.UI.ViewModels;

public class ToolboxViewModel
{
    public ObservableCollection<ToolboxGroupViewModel> Groups { get; }

    public ToolboxViewModel(OperatorRegistry registry, Action<IOperatorType> addAction)
    {
        var groups = registry.GetAll()
            .GroupBy(op => op.Category)
            .OrderBy(g => g.Key)
            .Select(g => new ToolboxGroupViewModel(
                g.Key,
                g.Select(op => new ToolboxItemViewModel(op, addAction))))
            .ToList();

        Groups = new ObservableCollection<ToolboxGroupViewModel>(groups);
    }
}
