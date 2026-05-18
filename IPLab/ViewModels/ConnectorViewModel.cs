using System.Windows;

namespace IPLab.ViewModels;

public class ConnectorViewModel : ViewModelBase
{
    public string Name  { get; }
    public string Title { get; }

    private Point _anchor;
    public Point Anchor
    {
        get => _anchor;
        set { _anchor = value; RaisePropertyChanged(); }
    }

    public ConnectorViewModel(string name, string title)
    {
        Name  = name;
        Title = title;
    }
}
