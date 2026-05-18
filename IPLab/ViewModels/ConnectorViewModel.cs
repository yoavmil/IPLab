using System.Windows;

namespace IPLab.ViewModels;

public class ConnectorViewModel : ViewModelBase
{
    public string         Name { get; }
    public ConnectionSide Side { get; }

    private Point _anchor;
    public Point Anchor
    {
        get => _anchor;
        set { _anchor = value; RaisePropertyChanged(); }
    }

    public ConnectorViewModel(string name, ConnectionSide side)
    {
        Name = name;
        Side = side;
    }
}
