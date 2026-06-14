using IPLab.Core.Models;
using IPLab.Core.Utilities;
using IPLab.UI.Services;
using OpenCvSharp;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace IPLab.UI.ViewModels;

public class InspectorViewModel : ViewModelBase
{
    private readonly ExecutionService _execution;

    private InspectorState _state = new();
    public InspectorState State
    {
        get => _state;
        private set { _state = value; RaisePropertyChanged(); }
    }

    private readonly ObservableCollection<LayerViewModel> _overlayLayers = [];
    public ObservableCollection<LayerViewModel> OverlayLayers => _overlayLayers;

    private readonly Dictionary<string, List<LayerViewModel>> _layersCache = new();
    private Dictionary<(string Id, string Port), BitmapSource> _precomputedImages = new();
    private Dictionary<(string Id, string Port), Mat?>        _precomputedMats    = new();

    private List<ParameterEditViewModel> _roiParamSubscriptions = [];

    private OperatorNodeViewModel? _selectedNode;
    private OperatorNodeViewModel? _editingNode;

    public InspectorViewModel(ExecutionService execution)
    {
        _execution = execution;
    }

    public void SetSelectedNode(OperatorNodeViewModel? node, FlowViewModel? flow)
    {
        _selectedNode = node;
        UpdateSelectedImage(flow);
    }

    public void SetEditingNode(OperatorNodeViewModel? node)
    {
        foreach (var p in _roiParamSubscriptions)
            p.PropertyChanged -= OnRoiParamChanged;
        _roiParamSubscriptions.Clear();

        _editingNode = node;

        if (_editingNode is not null)
        {
            _roiParamSubscriptions = _editingNode.Parameters
                .Where(p => p.Name is "RoiCX" or "RoiCY" or "RoiW" or "RoiH" or "RoiAngle")
                .ToList();
            foreach (var p in _roiParamSubscriptions)
                p.PropertyChanged += OnRoiParamChanged;
        }

        RefreshRoiOverlay();
    }

    private void OnRoiParamChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ParameterEditViewModel.ValueText))
            RefreshRoiOverlay();
    }

    private void RefreshRoiOverlay() => State = State with { Roi = CurrentRoi };

    private RoiDef? CurrentRoi
    {
        get
        {
            if (_editingNode is null) return null;
            double GetVal(string name)
            {
                var p = _editingNode.Parameters.FirstOrDefault(p => p.Name == name);
                return p is null ? 0.0 : ResolveParamAsDouble(p);
            }
            var cx    = GetVal("RoiCX");
            var cy    = GetVal("RoiCY");
            var w     = GetVal("RoiW");
            var h     = GetVal("RoiH");
            var angle = GetVal("RoiAngle");
            return (w > 0 && h > 0) ? new RoiDef(cx, cy, w, h, angle) : null;
        }
    }

    private double ResolveParamAsDouble(ParameterEditViewModel p)
    {
        if (p.IsWired && p.SelectedSource is { } src &&
            _execution.IntermediateResults.TryGetValue(src.OperatorId, out var result) &&
            result is Dictionary<string, object?> dict &&
            dict.TryGetValue(src.Port, out var raw) && raw is IConvertible conv)
            return conv.ToDouble(null);
        return double.TryParse(p.ValueText, out var v) ? v : 0.0;
    }

    public void UpdateSelectedImage(FlowViewModel? flow)
    {
        var newState = BuildState();
        if (newState != State)
            State = newState;
        UpdateOverlayLayers(flow);
    }

    private InspectorState BuildState()
    {
        if (_selectedNode is null || !_execution.HasResults)
            return new InspectorState() with { Roi = CurrentRoi };

        _execution.IntermediateResults.TryGetValue(_selectedNode.Id, out var result);

        var circles = Unwrap<CircleSegment[]>(result);
        if (circles is not null)
            return new InspectorState(Image: GetSourceImage(), Circles: circles) with { Roi = CurrentRoi };

        var blobs = Unwrap<KeyPoint[]>(result);
        if (blobs is not null)
            return new InspectorState(Image: GetSourceImage(), Blobs: blobs) with { Roi = CurrentRoi };

        var contours = Unwrap<OpenCvSharp.Point[][]>(result);
        if (contours is not null)
            return new InspectorState(Image: GetSourceImage(), Contours: contours) with { Roi = CurrentRoi };

        // Lines and crosses can co-exist (e.g. DistortionCalibration emits both grid lines and grid
        // corners), so handle them together rather than returning on the first match. Image falls
        // back to the wired source when the operator produces no display image (heatmap off).
        var lines   = Unwrap<LineSegmentPoint[]>(result);
        var crosses = Unwrap<Point2f[]>(result);
        if (lines is not null || crosses is not null)
            return new InspectorState(
                Image: GetDisplayImage() ?? GetSourceImage(),
                Lines: lines,
                Crosses: crosses) with { Roi = CurrentRoi };

        return new InspectorState(Image: GetDisplayImage()) with { Roi = CurrentRoi };
    }

    private static T? Unwrap<T>(object? result) where T : class
        => result as T
           ?? (result as Dictionary<string, object?>)?.Values.OfType<T>().FirstOrDefault();

    private BitmapSource? GetSourceImage()
    {
        var imageParam = _selectedNode!.Parameters
            .FirstOrDefault(p => p.Name == "Image" && p.IsWired && p.SelectedSource is not null);
        if (imageParam is null) return null;
        return _precomputedImages.GetValueOrDefault(
            (imageParam.SelectedSource!.OperatorId, imageParam.SelectedSource.Port));
    }

    private BitmapSource? GetDisplayImage()
    {
        var displayPort = _selectedNode!.Operator.Type.OutputPorts.FirstOrDefault(p => p.IsDisplayImage);
        return displayPort is not null
            ? _precomputedImages.GetValueOrDefault((_selectedNode.Id, displayPort.Name))
            : null;
    }

    public async Task PrecomputeAsync(FlowViewModel flow)
    {
        if (!_execution.HasResults) { _precomputedImages = new(); _precomputedMats = new(); return; }

        var results     = _execution.IntermediateResults;
        var portsByNode = flow.Nodes.ToDictionary(n => n.Id, n => n.Operator.Type.OutputPorts);
        var oldImages   = _precomputedImages;
        var oldMats     = _precomputedMats;

        var (newImages, newMats) = await Task.Run(() =>
        {
            var images = new Dictionary<(string, string), BitmapSource>();
            var mats   = new Dictionary<(string, string), Mat?>();

            foreach (var (id, result) in results)
            {
                var ports = portsByNode.TryGetValue(id, out var p) ? p
                    : (IReadOnlyList<OutputPortDescriptor>)[new OutputPortDescriptor { Name = "Image", DataType = typeof(Mat) }];
                if (result is Mat mat)
                {
                    var displayPort = ports.FirstOrDefault(p => p.IsDisplayImage);
                    if (displayPort is not null)
                        TryAddImage(images, mats, oldImages, oldMats, id, displayPort.Name, mat);
                }
                else if (result is Dictionary<string, object?> dict)
                {
                    foreach (var port in ports.Where(p => p.IsDisplayImage))
                    {
                        if (!dict.TryGetValue(port.Name, out var val)) continue;
                        TryAddImage(images, mats, oldImages, oldMats, id, port.Name, val as Mat);
                    }
                }
            }
            return (images, mats);
        });

        _precomputedImages = newImages;
        _precomputedMats   = newMats;
    }

    private static void TryAddImage(
        Dictionary<(string, string), BitmapSource> images,
        Dictionary<(string, string), Mat?> mats,
        Dictionary<(string, string), BitmapSource> oldImages,
        Dictionary<(string, string), Mat?> oldMats,
        string id, string port, Mat? mat)
    {
        var key = (id, port);
        if (mat is not null &&
            oldMats.TryGetValue(key, out var prevMat) && ReferenceEquals(prevMat, mat) &&
            oldImages.TryGetValue(key, out var prevBmp))
        {
            images[key] = prevBmp;
            mats[key]   = mat;
            return;
        }
        var bytes = ImageHelper.TryGetPngBytes(mat);
        if (bytes is not null)
        {
            images[key] = BytesToBitmapSource(bytes);
            mats[key]   = mat;
        }
    }

    public void RefreshCachedLayerImages(FlowViewModel flow)
    {
        var existingIds = flow.Nodes.Select(n => n.Id).ToHashSet();
        foreach (var id in _layersCache.Keys.Where(id => !existingIds.Contains(id)).ToList())
            _layersCache.Remove(id);

        foreach (var layers in _layersCache.Values)
            foreach (var layer in layers)
                layer.Image = _precomputedImages.GetValueOrDefault((layer.OperatorId, layer.Port));
    }

    private void UpdateOverlayLayers(FlowViewModel? flow)
    {
        if (_selectedNode is null || !_execution.HasResults)
        {
            _overlayLayers.Clear();
            return;
        }

        if (flow is null) return;

        if (!_layersCache.TryGetValue(_selectedNode.Id, out var layers))
            _layersCache[_selectedNode.Id] = layers = BuildLayersForNode(_selectedNode, flow);

        if (_overlayLayers.SequenceEqual(layers)) return;

        _overlayLayers.Clear();
        foreach (var layer in layers)
            _overlayLayers.Add(layer);
    }

    private List<LayerViewModel> BuildLayersForNode(OperatorNodeViewModel node, FlowViewModel flow)
    {
        var layers = new List<LayerViewModel>();

        var edges = flow.Connections
            .Select(c => (
                Source: flow.Nodes.FirstOrDefault(n => n.HasConnector(c.Source))?.Id,
                Target: flow.Nodes.FirstOrDefault(n => n.HasConnector(c.Target))?.Id))
            .Where(e => e.Source is not null && e.Target is not null)
            .Select(e => (e.Source!, e.Target!));

        var ancestorIds = FlowGraph.GetAncestors(node.Id, edges);
        var orderedAncestors = FlowGraph.TopologicalSort(ancestorIds, edges)
            .Select(id => flow.Nodes.FirstOrDefault(n => n.Id == id))
            .OfType<OperatorNodeViewModel>();

        foreach (var ancestor in orderedAncestors)
        {
            foreach (var (port, bitmap) in ExtractImageLayers(ancestor.Id, ancestor.Operator.Type.OutputPorts))
            {
                var label = ancestor.Operator.Type.OutputPorts.Count > 1
                    ? $"{ancestor.DisplayName} / {port}"
                    : ancestor.DisplayName;
                layers.Add(new LayerViewModel(ancestor.Id, port, label) { Image = bitmap });
            }
        }

        var ownImageLayers = ExtractImageLayers(node.Id, node.Operator.Type.OutputPorts).ToList();
        if (ownImageLayers.Count > 0)
        {
            foreach (var (port, bitmap) in ownImageLayers)
            {
                var label = ownImageLayers.Count > 1
                    ? $"{node.DisplayName} / {port}"
                    : node.DisplayName;
                layers.Add(new LayerViewModel(node.Id, port, label, isOwnLayer: true) { Image = bitmap });
            }
        }
        else
        {
            var imageParam = node.Parameters
                .FirstOrDefault(p => p.Name == "Image" && p.IsWired && p.SelectedSource is not null);
            if (imageParam is not null)
            {
                var inputLayer = layers.FirstOrDefault(l => l.OperatorId == imageParam.SelectedSource!.OperatorId);
                if (inputLayer is not null)
                    inputLayer.IsEnabled = true;
            }
        }
        return layers;
    }

    private IEnumerable<(string Port, BitmapSource Bitmap)> ExtractImageLayers(
        string operatorId, IReadOnlyList<OutputPortDescriptor> outputPorts)
    {
        foreach (var port in outputPorts)
            if (_precomputedImages.TryGetValue((operatorId, port.Name), out var bitmap))
                yield return (port.Name, bitmap);
    }

    public void Clear()
    {
        _overlayLayers.Clear();
        _layersCache.Clear();
        _precomputedImages = new();
        _precomputedMats   = new();
        State = new InspectorState();
    }

    private static BitmapSource BytesToBitmapSource(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption  = BitmapCacheOption.OnLoad;
        bi.StreamSource = ms;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }
}
