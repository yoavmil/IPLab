using System.Collections.ObjectModel;

namespace IPLab.UI.ViewModels;

public class DataNodeViewModel : ViewModelBase
{
    private Func<IEnumerable<DataNodeViewModel>>? _childFactory;

    public string Name      { get; }
    public string ValueText { get; }
    public string TypeText  { get; }

    public ObservableCollection<DataNodeViewModel> Children { get; } = [];

    // True when this node can be expanded (has a factory or already-built children).
    // Stored at construction so context menu visibility is stable.
    public bool HasChildren { get; }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            if (value && _childFactory is not null)
            {
                Children.Clear(); // remove placeholder
                foreach (var child in _childFactory())
                    Children.Add(child);
                _childFactory = null;
            }
            RaisePropertyChanged();
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; RaisePropertyChanged(); }
    }

    public DataNodeViewModel(string name, string valueText, string typeText,
        Func<IEnumerable<DataNodeViewModel>>? childFactory = null)
    {
        Name          = name;
        ValueText     = valueText;
        TypeText      = typeText;
        _childFactory = childFactory;
        HasChildren   = childFactory is not null;

        // Placeholder child: makes WPF show the expand arrow for lazy nodes
        // without building real children yet. Replaced on first expand.
        if (_childFactory is not null)
            Children.Add(Placeholder);
    }

    // Shared sentinel — never rendered (empty strings, never selected).
    internal static readonly DataNodeViewModel Placeholder = new("", "", "");
}
