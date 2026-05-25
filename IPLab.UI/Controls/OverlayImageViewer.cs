using IPLab.UI.ViewModels;
using RControls;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
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
        _canvas = GetTemplateChild(ElementMainCanvas) as Canvas;
        RefreshOverlays();
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
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);

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
