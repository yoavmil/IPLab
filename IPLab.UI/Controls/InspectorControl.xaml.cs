using IPLab.UI.ViewModels;
using IPLab.Core.Spatial;
using OpenCvSharp;
using RControls;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IPLab.UI.Controls;

public partial class InspectorControl : UserControl
{
    private MainViewModel? _vm;
    private const string DefaultPixelInfo = "Click on image to inspect pixel";

    public InspectorControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ImageViewer.ImageClicked += OnImageClicked;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = e.NewValue as MainViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;

        PixelInfoText.Text = DefaultPixelInfo;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.State))
            RedrawAnnotations(_vm!.State);
    }

    private void RedrawAnnotations(InspectorState state)
    {
        ImageViewer.RemoveRegion(string.Empty, ShapeMode.Circle);
        ImageViewer.RemoveRegion(string.Empty, ShapeMode.Rectangle);
        ImageViewer.RemoveRegion(string.Empty, ShapeMode.Cross);
        ImageViewer.RemoveRegion(string.Empty, ShapeMode.Polygon);
        ImageViewer.RemoveRegion(string.Empty, ShapeMode.Line);

        DrawCircles(state.Circles);
        DrawBlobs(state.Blobs);
        DrawContours(state.Contours);
        DrawLines(state.Lines);
        DrawCrosses(state.Crosses);
        DrawRectangles(state.Rectangles);
        DrawRoi(state.Roi);
    }

    private void DrawCircles(CircleSegment[]? circles)
    {
        if (circles is null) return;
        foreach (var c in circles)
            ImageViewer.DrawCircle(c.Center.Y, c.Center.X, c.Radius,
                                   string.Empty, Brushes.Lime, bFilled: false);
    }

    private void DrawBlobs(KeyPoint[]? blobs)
    {
        if (blobs is null) return;
        foreach (var b in blobs)
            ImageViewer.DrawCircle(b.Pt.Y, b.Pt.X, b.Size / 2.0,
                                   string.Empty, Brushes.Cyan, bFilled: false);
    }

    private void DrawContours(Point2f[][]? contours)
    {
        if (contours is null) return;
        var polygons = contours
            .Select(c => c.Select(p => new System.Windows.Point(p.X, p.Y)).ToList())
            .Where(pts => pts.Count >= 3)
            .ToList();
        if (polygons.Count == 0) return;
        ImageViewer.DrawPolygons(polygons, string.Empty, Brushes.Yellow);
    }

    private void DrawLines(LineSegment2f[]? lines)
    {
        if (lines is null || lines.Length == 0) return;
        var rowBegin    = lines.Select(l => (double)l.P1.Y).ToArray();
        var columnBegin = lines.Select(l => (double)l.P1.X).ToArray();
        var rowEnd      = lines.Select(l => (double)l.P2.Y).ToArray();
        var columnEnd   = lines.Select(l => (double)l.P2.X).ToArray();
        ImageViewer.DrawLine(rowBegin, columnBegin, rowEnd, columnEnd, string.Empty, Brushes.Orange, bFilled: false);
    }

    private void DrawCrosses(Point2f[]? crosses)
    {
        if (crosses is null) return;
        foreach (var pt in crosses)
            ImageViewer.DrawCross(pt.Y, pt.X, 0, string.Empty, 10, Brushes.Lime);
    }

    private void DrawRectangles(OpenCvSharp.Rect[]? rectangles)
    {
        if (rectangles is null) return;
        foreach (var rect in rectangles)
            ImageViewer.DrawRectangle(rect.Top, rect.Left, rect.Bottom, rect.Right,
                string.Empty, Brushes.Lime, bFilled: false);
    }

    private void DrawRoi(IPLab.Core.Models.RoiDef? roi)
    {
        if (roi is not { Width: > 0, Height: > 0 }) return;
        // User Angle convention: positive = right-up (CCW); RotatedRect uses CW positive, so negate.
        var rr = new RotatedRect(
            new Point2f((float)roi.CX, (float)roi.CY),
            new Size2f((float)roi.Width, (float)roi.Height),
            (float)-roi.Angle);
        var pts = rr.Points()
            .Select(p => new System.Windows.Point(p.X, p.Y))
            .ToList();
        ImageViewer.DrawPolygon(pts, "ROI", Brushes.Yellow, bFilled: false);

        // Arrow along the scan axis (W direction): center → right edge, with arrowhead.
        // Scan direction in image space: (cos θ, −sin θ) for CCW angle θ.
        double θ      = roi.Angle * Math.PI / 180.0;
        double dirX   = Math.Cos(θ);
        double dirY   = -Math.Sin(θ);
        double tipX   = roi.CX + dirX * roi.Width / 2.0;
        double tipY   = roi.CY + dirY * roi.Width / 2.0;
        double headLen = Math.Min(roi.Width * 0.2, 20.0);
        const double HeadAngle = Math.PI * 5.0 / 6.0; // 150° from forward = 30° opening
        double w1X = tipX + headLen * Math.Cos(θ + HeadAngle);
        double w1Y = tipY - headLen * Math.Sin(θ + HeadAngle);
        double w2X = tipX + headLen * Math.Cos(θ - HeadAngle);
        double w2Y = tipY - headLen * Math.Sin(θ - HeadAngle);

        ImageViewer.DrawLine(
            [roi.CY, tipY,  tipY],
            [roi.CX, tipX,  tipX],
            [tipY,   w1Y,   w2Y],
            [tipX,   w1X,   w2X],
            string.Empty, Brushes.Yellow, bFilled: false);
    }

    private void OnImageClicked(System.Windows.Point imagePoint)
    {
        if (_vm?.State.Image is not BitmapSource bmp)
        {
            PixelInfoText.Text = DefaultPixelInfo;
            return;
        }

        int x = (int)imagePoint.X;
        int y = (int)imagePoint.Y;

        if (x < 0 || y < 0 || x >= bmp.PixelWidth || y >= bmp.PixelHeight)
        {
            PixelInfoText.Text = DefaultPixelInfo;
            return;
        }

        int bytesPerPixel = Math.Max(1, (bmp.Format.BitsPerPixel + 7) / 8);
        var pixels = new byte[bytesPerPixel];
        bmp.CopyPixels(new Int32Rect(x, y, 1, 1), pixels, bytesPerPixel, 0);

        PixelInfoText.Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
        PixelInfoText.Text = FormatPixelInfo(x, y, bmp.Format, pixels);
    }

    private static string FormatPixelInfo(int x, int y, PixelFormat fmt, byte[] px)
    {
        var pos = $"({x}, {y})";

        if (fmt == PixelFormats.Gray8)
            return $"{pos}  │  {px[0],3}";

        if (fmt == PixelFormats.Gray16)
            return $"{pos}  │  {BitConverter.ToUInt16(px, 0),5}";

        if (fmt == PixelFormats.Bgr24 || fmt == PixelFormats.Bgr32)
            return $"{pos}  │  {px[2],3} {px[1],3} {px[0],3}";

        if (fmt == PixelFormats.Bgra32)
            return $"{pos}  │  {px[2],3} {px[1],3} {px[0],3} {px[3],3}";

        if (fmt == PixelFormats.Rgb24)
            return $"{pos}  │  {px[0],3} {px[1],3} {px[2],3}";

        return $"{pos}  │  {string.Join(" ", px.Select(b => b.ToString("X2")))}";
    }
}
