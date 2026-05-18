namespace IPLab.ViewModels;

public class ConnectionViewModel
{
    public ConnectorViewModel Source { get; }
    public ConnectorViewModel Target { get; }

    public ConnectionViewModel(ConnectorViewModel source, ConnectorViewModel target)
    {
        Source = source;
        Target = target;
    }
}
