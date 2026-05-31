using IPLab.UI.ViewModels;
using RControls;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace IPLab.UI.Controls;

/// <summary>
/// Extends ImageViewer with a stack of semi-transparent overlay images.
/// Each layer is a WPF Image element inside the canvas, so pan/zoom and
/// opacity changes are handled by WPF's GPU compositor — no CPU recompositing.
/// </summary>
public class OverlayImageViewer : ImageViewer
{
    private Canvas? _canvas;
    private Image? _mainImage;
    private const string LayerTag = "__iplab_layer__";

    public static readonly DependencyProperty OverlayLayersProperty =
        DependencyProperty.Register(
            nameof(OverlayLayers),
            typeof(ObservableCollection<LayerViewModel>),
            typeof(OverlayImageViewer),
            new PropertyMetadata(null, OnLayersChanged));

    public ObservableCollection<LayerViewModel>? OverlayLayers
    {
        get => (ObservableCollection<LayerViewModel>?)GetValue(OverlayLayersProperty);
        set => SetValue(OverlayLayersProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _canvas    = GetTemplateChild(ElementMainCanvas) as Canvas;
        _mainImage = GetTemplateChild(ElementMainImage)  as Image;

        if (_canvas != null)
        {
            // Fire UpdateScalingMode every time a zoom or pan replaces the canvas transform.
            var desc = DependencyPropertyDescriptor.FromProperty(UIElement.RenderTransformProperty, typeof(Canvas));
            desc.AddValueChanged(_canvas, OnTransformChanged);
            Unloaded += (_, _) => desc.RemoveValueChanged(_canvas!, OnTransformChanged);
        }

        RefreshOverlays();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        CenterIfSmaller();
    }

    private void OnTransformChanged(object? sender, EventArgs e) => UpdateScalingMode();

    // NearestNeighbor when zoomed in (crisp individual pixels).
    // HighQuality when zoomed out so thin features are never dropped by nearest-neighbor sampling.
    private void UpdateScalingMode()
    {
        var mode = scaleRatio >= 1.0 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality;
        if (_mainImage != null)
            RenderOptions.SetBitmapScalingMode(_mainImage, mode);
        if (_canvas != null)
            foreach (var img in _canvas.Children.OfType<Image>().Where(i => i.Tag as string == LayerTag))
                RenderOptions.SetBitmapScalingMode(img, mode);
    }

    // When the viewport is resized and the scaled image fits inside it, keep the image centered
    // so it doesn't drift to the top-left corner after the user zooms out or the panel is resized.
    private void CenterIfSmaller()
    {
        if (_canvas is null || SourceImage is null || scaleRatio <= 0) return;

        var transform = _canvas.RenderTransform as MatrixTransform;
        if (transform is null) return;

        var matrix    = transform.Matrix;
        double scaledW = SourceImage.Width  * scaleRatio;
        double scaledH = SourceImage.Height * scaleRatio;
        bool changed   = false;

        if (scaledW < ActualWidth)
        {
            matrix.OffsetX = (ActualWidth  - scaledW) / 2.0;
            changed = true;
        }
        if (scaledH < ActualHeight)
        {
            matrix.OffsetY = (ActualHeight - scaledH) / 2.0;
            changed = true;
        }
        if (changed)
            _canvas.RenderTransform = new MatrixTransform(matrix);
    }

    private static void OnLayersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (OverlayImageViewer)d;
        if (e.OldValue is ObservableCollection<LayerViewModel> old)
            old.CollectionChanged -= viewer.OnCollectionChanged;
        if (e.NewValue is ObservableCollection<LayerViewModel> @new)
            @new.CollectionChanged += viewer.OnCollectionChanged;
        viewer.RefreshOverlays();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshOverlays();

    private void RefreshOverlays()
    {
        if (_canvas is null) return;

        // Remove previous layer images (leave PART_MainImage and ImageItem shapes intact).
        var toRemove = _canvas.Children.OfType<Image>()
            .Where(img => img.Tag as string == LayerTag)
            .ToList();
        foreach (var img in toRemove)
            _canvas.Children.Remove(img);

        var layers = OverlayLayers;
        if (layers is null) return;

        var mode = scaleRatio >= 1.0 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality;

        // Insert after PART_MainImage (index 0), before any existing ImageItem shapes.
        int insertAt = 1;
        foreach (var layer in layers)
        {
            var img = new Image
            {
                Tag              = LayerTag,
                Stretch          = Stretch.None,
                IsHitTestVisible = false,
            };
            RenderOptions.SetBitmapScalingMode(img, mode);

            img.SetBinding(Image.SourceProperty,
                new Binding(nameof(LayerViewModel.Image)) { Source = layer });
            img.SetBinding(UIElement.OpacityProperty,
                new Binding(nameof(LayerViewModel.Opacity)) { Source = layer });
            img.SetBinding(UIElement.VisibilityProperty,
                new Binding(nameof(LayerViewModel.IsEnabled))
                {
                    Source    = layer,
                    Converter = new BooleanToVisibilityConverter(),
                });

            Canvas.SetLeft(img, 0);
            Canvas.SetTop(img, 0);
            _canvas.Children.Insert(Math.Min(insertAt++, _canvas.Children.Count), img);
        }
    }
}
