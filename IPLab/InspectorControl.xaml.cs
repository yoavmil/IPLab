using IPLab.ViewModels;
using OpenCvSharp;
using RControls;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IPLab;

public partial class InspectorControl : UserControl
{
    private MainViewModel? _vm;

    public InspectorControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = e.NewValue as MainViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.SelectedImage):
                ImageViewer.RemoveRegion(string.Empty, ShapeMode.Circle);
                ImageViewer.RemoveRegion(string.Empty, ShapeMode.Polygon);
                break;
            case nameof(MainViewModel.SelectedCircles):
                DrawCircles(_vm!.SelectedCircles);
                break;
            case nameof(MainViewModel.SelectedBlobs):
                DrawBlobs(_vm!.SelectedBlobs);
                break;
            case nameof(MainViewModel.SelectedContours):
                DrawContours(_vm!.SelectedContours);
                break;
        }
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

    private void DrawContours(OpenCvSharp.Point[][]? contours)
    {
        if (contours is null) return;
        foreach (var contour in contours)
        {
            if (contour.Length < 2) continue;
            var pts = contour.Select(p => new System.Windows.Point(p.X, p.Y)).ToList();
            ImageViewer.DrawPolygon(pts, string.Empty, Brushes.Yellow, bFilled: false);
        }
    }
}
