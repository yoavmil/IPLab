using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using System.ComponentModel;
using System.Collections.ObjectModel;
using ConnectedComponentInfo = IPLab.Core.Models.ConnectedComponentInfo;
using OperatorStatus = IPLab.Core.Models.OperatorStatus;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using IPLab.Core.Serialization;
using IPLab.Core.Utilities;
using Microsoft.Win32;
using OpenCvSharp;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CoreFlow = IPLab.Core.Runtime.Flow;

namespace IPLab.UI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private FlowViewModel _flow = null!;
    public FlowViewModel Flow
    {
        get => _flow;
        private set { _flow = value; RaisePropertyChanged(); }
    }

    private string _status = "Ready";
    public string Status
    {
        get => _status;
        private set { _status = value; RaisePropertyChanged(); }
    }

    private OperatorNodeViewModel? _selectedNode;
    public OperatorNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set { _selectedNode = value; RaisePropertyChanged(); UpdateSelectedImage(); }
    }

    public sealed record InspectorState(
        BitmapSource?             Image      = null,
        CircleSegment[]?          Circles    = null,
        KeyPoint[]?               Blobs      = null,
        ConnectedComponentInfo[]? Components = null,
        OpenCvSharp.Point[][]?    Contours   = null,
        RoiDef?                   Roi        = null);

    private InspectorState _state = new();
    public  InspectorState State
    {
        get => _state;
        private set { _state = value; RaisePropertyChanged(); }
    }

    public event Action<OperatorNodeViewModel?>? EditingNodeChanged;

    private List<ParameterEditViewModel> _roiParamSubscriptions = [];

    private OperatorNodeViewModel? _editingNode;
    public OperatorNodeViewModel? EditingNode
    {
        get => _editingNode;
        private set
        {
            foreach (var p in _roiParamSubscriptions)
                p.PropertyChanged -= OnRoiParamChanged;
            _roiParamSubscriptions.Clear();

            _editingNode = value;

            if (_editingNode is not null)
            {
                _roiParamSubscriptions = _editingNode.Parameters
                    .Where(p => p.Name is "RoiX" or "RoiY" or "RoiW" or "RoiH")
                    .ToList();
                foreach (var p in _roiParamSubscriptions)
                    p.PropertyChanged += OnRoiParamChanged;
            }

            RefreshRoiOverlay();
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsSettingsPanelOpen));
            EditingNodeChanged?.Invoke(value);
        }
    }

    private void OnRoiParamChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ParameterEditViewModel.ValueText))
            RefreshRoiOverlay();
    }

    private void RefreshRoiOverlay() => State = State with { Roi = CurrentRoi };

    private int ResolveRoiParam(ParameterEditViewModel p)
    {
        if (p.IsWired && p.SelectedSource is { } src && _executor is not null &&
            _executor.IntermediateResults.TryGetValue(src.OperatorId, out var result) &&
            result is Dictionary<string, object?> dict &&
            dict.TryGetValue(src.Port, out var raw) && raw is IConvertible conv)
            return conv.ToInt32(null);
        return int.TryParse(p.ValueText, out var v) ? v : 0;
    }

    private RoiDef? CurrentRoi
    {
        get
        {
            if (_editingNode is null) return null;
            int GetVal(string name)
            {
                var p = _editingNode.Parameters.FirstOrDefault(p => p.Name == name);
                return p is null ? 0 : ResolveRoiParam(p);
            }
            var x = GetVal("RoiX");
            var y = GetVal("RoiY");
            var w = GetVal("RoiW");
            var h = GetVal("RoiH");
            return (w > 0 && h > 0) ? new RoiDef(x, y, w, h) : null;
        }
    }

    public bool IsSettingsPanelOpen => _editingNode is not null;

    // Lags behind EditingNode: updated by MainWindow only after the close animation
    // finishes, so the panel content doesn't vanish before it is hidden.
    private OperatorNodeViewModel? _displayingNode;
    public OperatorNodeViewModel? DisplayingNode
    {
        get => _displayingNode;
        set { _displayingNode = value; RaisePropertyChanged(); }
    }

    private readonly ObservableCollection<LayerViewModel> _overlayLayers = [];
    public  ObservableCollection<LayerViewModel> OverlayLayers => _overlayLayers;

    private readonly Dictionary<string, List<LayerViewModel>> _layersCache = new();

    private FlowEx? _executor;
    private CancellationTokenSource? _cts;
    private Dictionary<(string Id, string Port), BitmapSource> _precomputedImages = new();

    private bool _isRunningContinuous;
    public bool IsRunningContinuous
    {
        get => _isRunningContinuous;
        private set
        {
            _isRunningContinuous = value;
            RaisePropertyChanged();
            ((RelayCommand)RunOnceCommand).RaiseCanExecuteChanged();
        }
    }

    public ToolboxViewModel Toolbox           { get; }
    public ICommand         RunOnceCommand       { get; }
    public ICommand         RunContinuousCommand { get; }
    public ICommand         StopCommand          { get; }
    public ICommand         ClearResultsCommand  { get; }
    public ICommand         CloseSettingsCommand { get; }
    public ICommand         SaveFlowCommand      { get; }
    public ICommand         LoadFlowCommand      { get; }

    public MainViewModel()
    {
        Toolbox = new ToolboxViewModel(OperatorRegistry.CreateDefault(), type => Flow.AddNode(type));
        Flow = new FlowViewModel(BuildSampleFlow(),
            onOpenSettings: node => EditingNode = (EditingNode == node) ? null : node,
            onSelected:     node => SelectedNode = node);
        RunOnceCommand       = new RelayCommand(RunOnce, () => !IsRunningContinuous);
        RunContinuousCommand = new RelayCommand(ToggleContinuousRun);
        StopCommand          = new RelayCommand(Stop);
        ClearResultsCommand  = new RelayCommand(ClearResults);
        CloseSettingsCommand = new RelayCommand(() => EditingNode = null);
        SaveFlowCommand      = new RelayCommand(SaveFlow);
        LoadFlowCommand      = new RelayCommand(LoadFlow);
    }

    private async void RunOnce() => await ExecuteRunAsync();

    private void ToggleContinuousRun()
    {
        if (IsRunningContinuous)
        {
            Stop();
        }
        else
        {
            IsRunningContinuous = true;
            RunOnce();
        }
    }

    private async Task<bool> ExecuteRunAsync()
    {
        var filePathParam = Flow.Nodes
            .SelectMany(n => n.Parameters)
            .FirstOrDefault(p => p.Name == "FilePath");

        if (filePathParam is not null && string.IsNullOrEmpty(filePathParam.ValueText))
        {
            var dialog = new OpenFileDialog
            {
                Title  = "Select an image to process",
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|All files|*.*"
            };
            if (dialog.ShowDialog() != true) { IsRunningContinuous = false; return false; }
            filePathParam.ValueText = dialog.FileName;
        }

        Status = "Running…";
        _cts = new CancellationTokenSource();
        try
        {
            var flow = BuildExecutionFlow();
            _executor = new FlowEx(flow);
            _executor.StatusChanged += OnOperatorStatusChanged;
            await _executor.RunAllAsync(_cts.Token);
            await PrecomputeImagesAsync();
            RefreshCachedLayerImages();

            var failed = Flow.Nodes.Count(n => n.Status == OperatorStatus.Failed);
            Status = failed > 0
                ? $"{failed} operator(s) failed"
                : $"Done  |  {Path.GetFileName(filePathParam?.ValueText ?? string.Empty)}";
            UpdateSelectedImage();

            if (IsRunningContinuous)
                Application.Current?.Dispatcher.InvokeAsync(RunOnce, DispatcherPriority.Background);
            return true;
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped";
            return false;
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
            IsRunningContinuous = false;
            return false;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Stop()
    {
        IsRunningContinuous = false;
        _cts?.Cancel();
    }

    private void OnOperatorStatusChanged(string id, OperatorStatus status, Exception? ex)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            var node = Flow.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null) return;
            node.Status       = status;
            node.ErrorMessage = ex?.Message;
        }, DispatcherPriority.Background);
    }

    private void ClearResults()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts      = null;
        _executor = null;
        State     = new InspectorState();
        Status    = "Ready";

        _overlayLayers.Clear();
        _layersCache.Clear();
        _precomputedImages = new();

        foreach (var node in Flow.Nodes)
        {
            node.Status       = OperatorStatus.NotRun;
            node.ErrorMessage = null;
        }

        var filePathParam = Flow.Nodes
            .SelectMany(n => n.Parameters)
            .FirstOrDefault(p => p.Name == "FilePath");
        if (filePathParam is not null)
            filePathParam.ValueText = string.Empty;
    }

    private void SaveFlow()
    {
        var dialog = new SaveFileDialog
        {
            Title      = "Save Flow",
            Filter     = "IPLab Flow|*.ipl|JSON|*.json|All files|*.*",
            DefaultExt = ".ipl"
        };
        if (dialog.ShowDialog() != true) return;

        var execFlow = BuildExecutionFlow();
        var nodeById = Flow.Nodes.ToDictionary(n => n.Id);

        var opLayouts = Flow.Nodes
            .Select(n => new OperatorLayout(n.Id, new LayoutPoint(n.Location.X, n.Location.Y)));

        // Derive dep IDs from execFlow so they match the serialized FlowDef.
        // Multiple visual connections to the same parameter all share one dep ID —
        // use the first matching visual connection to get the connector sides.
        var depLayouts = execFlow.Operators
            .SelectMany(op => op.Dependencies, (op, dep) =>
            {
                var targetNode = nodeById.GetValueOrDefault(op.Id);
                var sourceNode = nodeById.GetValueOrDefault(dep.OperatorId);
                if (targetNode is null || sourceNode is null) return null;
                var conn = Flow.Connections.FirstOrDefault(
                    c => sourceNode.HasConnector(c.Source) && targetNode.HasConnector(c.Target));
                return conn is null ? null
                    : new DependencyLayout(dep.DependencyId, conn.Source.Side, conn.Target.Side);
            })
            .OfType<DependencyLayout>();

        IFlow flow = new CoreFlow(execFlow, new FlowLayout(opLayouts, depLayouts));
        File.WriteAllText(dialog.FileName, FlowDefSerializer.Serialize(flow));
        Status = $"Saved: {Path.GetFileName(dialog.FileName)}";
    }

    private void LoadFlow()
    {
        var dialog = new OpenFileDialog
        {
            Title  = "Open Flow",
            Filter = "IPLab Flow|*.ipl|JSON|*.json|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var flow = FlowDefSerializer.Deserialize(
                File.ReadAllText(dialog.FileName), OperatorRegistry.CreateDefault());

            EditingNode        = null;
            _executor          = null;
            State              = new InspectorState();
            _layersCache.Clear();
            _precomputedImages = new();
            Flow = new FlowViewModel(flow,
                onOpenSettings: node => EditingNode = (EditingNode == node) ? null : node,
                onSelected:     node => SelectedNode = node);
            Status = $"Loaded: {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            Status = $"Load failed: {ex.Message}";
        }
    }

    private void UpdateSelectedImage()
    {
        if (_selectedNode is null || _executor is null)
        {
            State = new InspectorState() with { Roi = CurrentRoi };
            UpdateOverlayLayers();
            return;
        }

        _executor.IntermediateResults.TryGetValue(_selectedNode.Id, out var result);

        var circles = Unwrap<CircleSegment[]>(result);
        if (circles is not null)
        {
            State = new InspectorState(Image: GetSourceImage(), Circles: circles) with { Roi = CurrentRoi };
            UpdateOverlayLayers();
            return;
        }

        var blobs = Unwrap<KeyPoint[]>(result);
        if (blobs is not null)
        {
            State = new InspectorState(Image: GetSourceImage(), Blobs: blobs) with { Roi = CurrentRoi };
            UpdateOverlayLayers();
            return;
        }

        var components = Unwrap<ConnectedComponentInfo[]>(result);
        if (components is not null)
        {
            var labelImage = _precomputedImages.GetValueOrDefault((_selectedNode.Id, "LabelImage"))
                          ?? GetSourceImage();
            State = new InspectorState(Image: labelImage, Components: components) with { Roi = CurrentRoi };
            UpdateOverlayLayers();
            return;
        }

        var contours = Unwrap<OpenCvSharp.Point[][]>(result);
        if (contours is not null)
        {
            State = new InspectorState(Image: GetSourceImage(), Contours: contours) with { Roi = CurrentRoi };
            UpdateOverlayLayers();
            return;
        }

        var image = _precomputedImages.Keys
            .Where(k => k.Id == _selectedNode.Id)
            .Select(k => _precomputedImages[k])
            .FirstOrDefault();
        State = new InspectorState(Image: image) with { Roi = CurrentRoi };
        UpdateOverlayLayers();
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

    private FlowDef BuildExecutionFlow() => new(
        Flow.Nodes.Select(node => new Operator
        {
            Id           = node.Operator.Id,
            DisplayName  = node.Operator.DisplayName,
            Type         = node.Operator.Type,
            Parameters   = node.Parameters.Select(p => p.ToParameterValue()).ToList(),
            // One Dependency per unique upstream operator — execution ordering only.
            // Parameter wiring lives in ParameterValue.Source, not here.
            Dependencies = node.Parameters
                .Where(p => p.IsWired && p.SelectedSource is not null)
                .GroupBy(p => p.SelectedSource!.OperatorId)
                .Select(g => new Dependency($"D_{g.Key}_{node.Id}", g.Key))
                .ToList()
        }));

    // Rebuilds OverlayLayers from the selected node's layers cache.
    // Called at every exit point of UpdateSelectedImage because that method returns
    // early from separate branches and has no single shared exit path.
    private void UpdateOverlayLayers()
    {
        if (_selectedNode is null || _executor is null)
        {
            _overlayLayers.Clear();
            return;
        }

        if (!_layersCache.TryGetValue(_selectedNode.Id, out var layers))
            _layersCache[_selectedNode.Id] = layers = BuildLayersForNode(_selectedNode);

        // On repeated runs with the same node selected the LayerViewModel objects are
        // identical (cached and image-refreshed in place), so skip the clear+re-add.
        // The clear causes a visible flash because RefreshOverlays removes canvas Image
        // elements synchronously, even though rendering is deferred.
        if (_overlayLayers.SequenceEqual(layers))
            return;

        _overlayLayers.Clear();
        foreach (var layer in layers)
            _overlayLayers.Add(layer);
    }

    // Encodes all executor results to frozen BitmapSources on a background thread.
    // Called once per run so all downstream consumers do cheap dict lookups on the UI thread.
    private async Task PrecomputeImagesAsync()
    {
        if (_executor is null) { _precomputedImages = new(); return; }

        var executor     = _executor;
        var portsByNode  = Flow.Nodes.ToDictionary(n => n.Id, n => n.Operator.Type.OutputPorts);

        _precomputedImages = await Task.Run(() =>
        {
            var images = new Dictionary<(string, string), BitmapSource>();
            foreach (var (id, result) in executor.IntermediateResults)
            {
                var ports = portsByNode.TryGetValue(id, out var p) ? p : (IReadOnlyList<string>)["Image"];
                if (result is Mat mat)
                {
                    var bytes = ImageHelper.TryGetPngBytes(mat);
                    if (bytes is not null)
                        images[(id, ports.Count > 0 ? ports[0] : "Image")] = BytesToBitmapSource(bytes);
                }
                else if (result is Dictionary<string, object?> dict)
                {
                    foreach (var (port, val) in dict)
                    {
                        var bytes = ImageHelper.TryGetPngBytes(val);
                        if (bytes is not null)
                            images[(id, port)] = BytesToBitmapSource(bytes);
                    }
                }
            }
            return images;
        });
    }

    // After a re-run, refreshes Image on every cached LayerViewModel using the precomputed dict.
    // Drops cache entries for nodes that were removed from the flow.
    private void RefreshCachedLayerImages()
    {
        var existingIds = Flow.Nodes.Select(n => n.Id).ToHashSet();
        foreach (var id in _layersCache.Keys.Where(id => !existingIds.Contains(id)).ToList())
            _layersCache.Remove(id);

        foreach (var layers in _layersCache.Values)
            foreach (var layer in layers)
                layer.Image = _precomputedImages.GetValueOrDefault((layer.OperatorId, layer.Port));
    }

    // Builds the layer list for a node once per run: ancestor images in topological order,
    // then the node's own image output (or its wired input image for annotation operators).
    // The returned LayerViewModel objects are cached so user toggle/opacity choices survive
    // node switching without a separate save/restore mechanism.
    private List<LayerViewModel> BuildLayersForNode(OperatorNodeViewModel node)
    {
        var layers = new List<LayerViewModel>();
        if (_executor is null) return layers;

        var edges = Flow.Connections
            .Select(c => (
                Source: Flow.Nodes.FirstOrDefault(n => n.HasConnector(c.Source))?.Id,
                Target: Flow.Nodes.FirstOrDefault(n => n.HasConnector(c.Target))?.Id))
            .Where(e => e.Source is not null && e.Target is not null)
            .Select(e => (e.Source!, e.Target!));

        var ancestorIds = FlowGraph.GetAncestors(node.Id, edges);
        var orderedAncestors = FlowGraph.TopologicalSort(ancestorIds, edges)
            .Select(id => Flow.Nodes.FirstOrDefault(n => n.Id == id))
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
            // Annotation operator (no image output, e.g. DetectCircles): mark the wired
            // input image's ancestor layer as enabled so it appears active in the panel.
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
        string operatorId, IReadOnlyList<string> outputPorts)
    {
        foreach (var port in outputPorts)
            if (_precomputedImages.TryGetValue((operatorId, port), out var bitmap))
                yield return (port, bitmap);
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

    private static IFlow BuildSampleFlow()
    {
        var flowDef = new FlowDef(
        [
            new Operator
            {
                Id           = "O1",
                DisplayName  = "Load Image",
                Type         = new LoadImageOperator(),
                Parameters   = [new ParameterValue { Name = "FilePath", Value = string.Empty }],
                Dependencies = []
            },
            new Operator
            {
                Id           = "O2",
                DisplayName  = "To Grayscale",
                Type         = new ConvertToGrayscaleOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",  Source = new SourceRef("O1", "Image") },
                    new ParameterValue { Name = "Method", Value  = "HsvValue" }
                ],
                Dependencies = [new Dependency("D_O1_O2", "O1")]
            },
            new Operator
            {
                Id           = "O3",
                DisplayName  = "Threshold",
                Type         = new ThresholdOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",  Source = new SourceRef("O2", "Image") },
                    new ParameterValue { Name = "Thresh", Value  = 128.0 }
                ],
                Dependencies = [new Dependency("D_O2_O3", "O2")]
            },
            new Operator
            {
                Id           = "O4",
                DisplayName  = "Detect Circles",
                Type         = new DetectCirclesOperator(),
                Parameters   =
                [
                    new ParameterValue { Name = "Image",     Source = new SourceRef("O3", "Image") },
                    new ParameterValue { Name = "MinDist",   Value  = 50.0  },
                    new ParameterValue { Name = "Param1",    Value  = 150.0 },
                    new ParameterValue { Name = "Param2",    Value  = 10.0  },
                    new ParameterValue { Name = "MinRadius", Value  = 10    },
                    new ParameterValue { Name = "MaxRadius", Value  = 100   }
                ],
                Dependencies = [new Dependency("D_O3_O4", "O3")]
            }
        ]);

        var layout = new FlowLayout(
            operators:
            [
                new OperatorLayout("O3", new LayoutPoint(240, 400)),
                new OperatorLayout("O4", new LayoutPoint(140, 580)),
            ],
            dependencies:
            [
                new DependencyLayout("D_O2_O3", ConnectionSide.Bottom, ConnectionSide.Left),
            ]);

        return new CoreFlow(flowDef, layout);
    }
}
