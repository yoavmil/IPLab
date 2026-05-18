using Nodify;

namespace IPLab.ViewModels;

public enum ConnectionSide { Left, Top, Right, Bottom }

public class ConnectionViewModel : ViewModelBase
{
    public ConnectorViewModel Source { get; }
    public ConnectorViewModel Target { get; }

    public ConnectorPosition SourcePosition => ToConnectorPosition(Source.Side);
    public ConnectorPosition TargetPosition => ToConnectorPosition(Target.Side);

    public ConnectionViewModel(ConnectorViewModel source, ConnectorViewModel target)
    {
        Source = source;
        Target = target;
    }

    private static ConnectorPosition ToConnectorPosition(ConnectionSide side) => side switch
    {
        ConnectionSide.Top    => ConnectorPosition.Top,
        ConnectionSide.Left   => ConnectorPosition.Left,
        ConnectionSide.Right  => ConnectorPosition.Right,
        _                     => ConnectorPosition.Bottom
    };
}
