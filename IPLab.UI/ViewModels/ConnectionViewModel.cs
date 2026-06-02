using IPLab.Core.Models;
using Nodify;
using System.Windows.Input;

namespace IPLab.UI.ViewModels;

public class ConnectionViewModel : ViewModelBase
{
    public ConnectorViewModel Source { get; }
    public ConnectorViewModel Target { get; }
    public string?            DependencyId { get; }
    public ICommand           DeleteCommand { get; }

    public ConnectorPosition SourcePosition => ToConnectorPosition(Source.Side);
    public ConnectorPosition TargetPosition => ToConnectorPosition(Target.Side);

    public ConnectionViewModel(ConnectorViewModel source, ConnectorViewModel target,
        string? dependencyId = null,
        Action<ConnectionViewModel>? onDelete = null)
    {
        Source        = source;
        Target        = target;
        DependencyId  = dependencyId;
        DeleteCommand = new RelayCommand(() => onDelete?.Invoke(this));
    }

    private static ConnectorPosition ToConnectorPosition(ConnectionSide side) => side switch
    {
        ConnectionSide.Top   => ConnectorPosition.Top,
        ConnectionSide.Left  => ConnectorPosition.Left,
        ConnectionSide.Right => ConnectorPosition.Right,
        _                    => ConnectorPosition.Bottom
    };
}
