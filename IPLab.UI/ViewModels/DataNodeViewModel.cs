using System.Collections.ObjectModel;

namespace IPLab.UI.ViewModels;

public class DataNodeViewModel : ViewModelBase
{
    private Func<IEnumerable<DataNodeViewModel>>? _childFactory;

    private readonly string        _path;
    private readonly ISet<string>? _expandedPaths; // null for Placeholder sentinel
    private bool                   _isExpanded;    // fallback when _expandedPaths is null

    public string Name      { get; }
    public string ValueText { get; }
    public string TypeText  { get; }

    public ObservableCollection<DataNodeViewModel> Children { get; } = [];

    public bool HasChildren { get; }

    public bool IsExpanded
    {
        get => _expandedPaths is not null ? _expandedPaths.Contains(_path) : _isExpanded;
        set
        {
            bool current = _expandedPaths is not null ? _expandedPaths.Contains(_path) : _isExpanded;
            if (current == value) return;

            if (_expandedPaths is not null)
            {
                if (value) _expandedPaths.Add(_path);
                else       _expandedPaths.Remove(_path);
            }
            else
            {
                _isExpanded = value;
            }

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
        Func<IEnumerable<DataNodeViewModel>>? childFactory = null,
        string path = "",
        ISet<string>? expandedPaths = null)
    {
        Name           = name;
        ValueText      = valueText;
        TypeText       = typeText;
        _childFactory  = childFactory;
        _path          = path;
        _expandedPaths = expandedPaths;
        HasChildren    = childFactory is not null;

        if (_childFactory is not null)
        {
            if (_expandedPaths is not null && _path.Length > 0 && _expandedPaths.Contains(_path))
            {
                // Auto-restore: path was previously expanded — materialise immediately so the
                // WPF TreeViewItem sees IsExpanded=true with real children already present.
                foreach (var child in _childFactory())
                    Children.Add(child);
                _childFactory = null;
            }
            else
            {
                Children.Add(Placeholder);
            }
        }
    }

    internal static readonly DataNodeViewModel Placeholder = new("", "", "");
}
