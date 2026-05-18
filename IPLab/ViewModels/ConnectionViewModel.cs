using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace IPLab.ViewModels;

public enum ConnectionSide { Left, Top, Right, Bottom }

public class ConnectionViewModel : ViewModelBase
{
    public ConnectorViewModel Source { get; }
    public ConnectorViewModel Target { get; }

    private const double ControlOffset = 80.0;

    public ConnectionViewModel(ConnectorViewModel source, ConnectorViewModel target)
    {
        Source = source;
        Target = target;

        source.PropertyChanged += OnAnchorChanged;
        target.PropertyChanged += OnAnchorChanged;
    }

    private void OnAnchorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectorViewModel.Anchor))
            RaisePropertyChanged(nameof(BezierGeometry));
    }

    public Geometry BezierGeometry
    {
        get
        {
            var p1 = ControlPoint(Source.Anchor, Source.Side);
            var p2 = ControlPoint(Target.Anchor, Target.Side);

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(Source.Anchor, false, false);
                ctx.BezierTo(p1, p2, Target.Anchor, true, false);
            }
            geometry.Freeze();
            return geometry;
        }
    }

    private static Point ControlPoint(Point anchor, ConnectionSide side) => side switch
    {
        ConnectionSide.Top    => new Point(anchor.X,                  anchor.Y - ControlOffset),
        ConnectionSide.Bottom => new Point(anchor.X,                  anchor.Y + ControlOffset),
        ConnectionSide.Left   => new Point(anchor.X - ControlOffset,  anchor.Y),
        ConnectionSide.Right  => new Point(anchor.X + ControlOffset,  anchor.Y),
        _                     => anchor
    };
}
