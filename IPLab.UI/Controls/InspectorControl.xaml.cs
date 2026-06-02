using IPLab.UI.ViewModels;
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

    private void RedrawAnnotations(MainViewModel.InspectorState state)
    {
        ImageViewer.RemoveRegion(string.Empty, ShapeMode.Circle);
        ImageViewer.RemoveRegion(string.Empty, ShapeMode.Rectangle);
        ImageViewer.RemoveRegion(string.Empty, ShapeMode.Cross);
        ImageViewer.RemoveRegion(string.Empty, ShapeMode.Polygon);

        if (state.Circles  is { } circles)  DrawCircles(circles);
        if (state.Blobs    is { } blobs)    DrawBlobs(blobs);
        if (state.Contours is { } contours) DrawContours(contours);
        if (state.Roi is { Width: > 0, Height: > 0 } roi)
            ImageViewer.DrawRectangle(roi.Y, roi.X, roi.Y + roi.Height, roi.X + roi.Width,
                                      "ROI", Brushes.Yellow, bFilled: false);
    }

    private void DrawCircles(CircleSegment[] circles)
    {
        foreach (var c in circles)
            ImageViewer.DrawCircle(c.Center.Y, c.Center.X, c.Radius,
                                   string.Empty, Brushes.Lime, bFilled: false);
    }

    private void DrawBlobs(KeyPoint[] blobs)
    {
        foreach (var b in blobs)
            ImageViewer.DrawCircle(b.Pt.Y, b.Pt.X, b.Size / 2.0,
                                   string.Empty, Brushes.Cyan, bFilled: false);
    }

    private void DrawContours(OpenCvSharp.Point[][] contours)
    {
        var polygons = contours
            .Select(c => c.Select(p => new System.Windows.Point(p.X, p.Y)).ToList())
            .Where(pts => pts.Count >= 3)
            .ToList();
        if (polygons.Count == 0) return;
        ImageViewer.DrawPolygons(polygons, string.Empty, Brushes.Yellow);
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
