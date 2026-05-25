using System.Collections.ObjectModel;

namespace IPLab.UI.ViewModels;

public class ToolboxGroupViewModel : ViewModelBase
{
    public string                                     Category { get; }
    public ObservableCollection<ToolboxItemViewModel> Items    { get; }

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; RaisePropertyChanged(); }
    }

    public ToolboxGroupViewModel(string category, IEnumerable<ToolboxItemViewModel> items)
    {
        Category = category;
        Items    = new ObservableCollection<ToolboxItemViewModel>(items);
    }
}
