using IPLab.Core.Interfaces;
using IPLab.Core.Models;
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
        OpenCvSharp.Point[][]?    Contours   = null);

    private InspectorState _state = new();
    public  InspectorState State
    {
        get => _state;
        private set { _state = value; RaisePropertyChanged(); }
    }

    private OperatorNodeViewModel? _editingNode;
    public OperatorNodeViewModel? EditingNode
    {
        get => _editingNode;
        private set
        {
            _editingNode = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsSettingsPanelOpen));
        }
    }

    public bool IsSettingsPanelOpen => _editingNode is not null;

    private readonly ObservableCollection<LayerViewModel> _overlayLayers = [];
    public  ObservableCollection<LayerViewModel> OverlayLayers => _overlayLayers;

    private readonly Dictionary<string, List<LayerViewModel>> _layersCache = new();

    private FlowEx? _executor;
    private CancellationTokenSource? _cts;

    public ToolboxViewModel Toolbox      { get; }
    public ICommand RunAllCommand        { get; }
    public ICommand StopCommand          { get; }
    public ICommand ClearResultsCommand  { get; }
    public ICommand CloseSettingsCommand { get; }
    public ICommand SaveFlowCommand      { get; }
    public ICommand LoadFlowCommand      { get; }

    public MainViewModel()
    {
        Toolbox = new ToolboxViewModel(OperatorRegistry.CreateDefault(), type => Flow.AddNode(type));
        Flow = new FlowViewModel(BuildSampleFlow(),
            onOpenSettings: node => EditingNode = node,
            onSelected:     node => SelectedNode = node);
        RunAllCommand        = new RelayCommand(RunAll);
        StopCommand          = new RelayCommand(Stop);
        ClearResultsCommand  = new RelayCommand(ClearResults);
        CloseSettingsCommand = new RelayCommand(() => EditingNode = null);
        SaveFlowCommand      = new RelayCommand(SaveFlow);
        LoadFlowCommand      = new RelayCommand(LoadFlow);
    }

    private async void RunAll()
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
            if (dialog.ShowDialog() != true) return;
            filePathParam.ValueText = dialog.FileName;
        }

        Status = "Running…";
        _cts = new CancellationTokenSource();
        try
        {
            var flow = BuildExecutionFlow();
            _layersCache.Clear();
            _executor = new FlowEx(flow);
            _executor.StatusChanged += OnOperatorStatusChanged;
            await _executor.RunAllAsync(_cts.Token);

            var failed = Flow.Nodes.Count(n => n.Status == OperatorStatus.Failed);
            Status = failed > 0
                ? $"{failed} operator(s) failed"
                : $"Done  |  {Path.GetFileName(filePathParam?.ValueText ?? string.Empty)}";
            UpdateSelectedImage();
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Stop() => _cts?.Cancel();

    private void OnOperatorStatusChanged(string id, OperatorStatus status, Exception? ex)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var node = Flow.Nodes.FirstOrDefault(n => n.Id == id);
            if (node is null) return;
            node.Status       = status;
            node.ErrorMessage = ex?.Message;
        });
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

            EditingNode = null;
            _executor   = null;
            State       = new InspectorState();
            _layersCache.Clear();
            Flow = new FlowViewModel(flow,
                onOpenSettings: node => EditingNode = node,
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
            State = new InspectorState();
            UpdateOverlayLayers();
            return;
        }

        _executor.IntermediateResults.TryGetValue(_selectedNode.Id, out var result);

        var circles = Unwrap<CircleSegment[]>(result);
        if (circles is not null)
        {
            State = new InspectorState(Image: GetSourceImage(), Circles: circles);
            UpdateOverlayLayers();
            return;
        }

        var blobs = Unwrap<KeyPoint[]>(result);
        if (blobs is not null)
        {
            State = new InspectorState(Image: GetSourceImage(), Blobs: blobs);
            UpdateOverlayLayers();
            return;
        }

        var components = Unwrap<ConnectedComponentInfo[]>(result);
        if (components is not null)
        {
            var labelMat   = (result as Dictionary<string, object?>)?.GetValueOrDefault("LabelImage") as OpenCvSharp.Mat;
            var labelBytes = labelMat is not null ? ImageHelper.TryGetPngBytes(labelMat) : null;
            var image      = labelBytes is not null ? BytesToBitmapSource(labelBytes) : GetSourceImage();
            State = new InspectorState(Image: image, Components: components);
            UpdateOverlayLayers();
            return;
        }

        var contours = Unwrap<OpenCvSharp.Point[][]>(result);
        if (contours is not null)
        {
            State = new InspectorState(Image: GetSourceImage(), Contours: contours);
            UpdateOverlayLayers();
            return;
        }

        // For multi-port operators, try each output value as an image.
        var imageSource = result is Dictionary<string, object?> dict
            ? dict.Values.FirstOrDefault(v => v is OpenCvSharp.Mat)
            : result;
        var bytes = ImageHelper.TryGetPngBytes(imageSource);
        State = new InspectorState(Image: bytes is not null ? BytesToBitmapSource(bytes) : null);
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

        _executor!.IntermediateResults.TryGetValue(imageParam.SelectedSource!.OperatorId, out var sourceResult);
        var bytes = ImageHelper.TryGetPngBytes(sourceResult);
        return bytes is not null ? BytesToBitmapSource(bytes) : null;
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
        _overlayLayers.Clear();
        if (_selectedNode is null || _executor is null) return;

        if (!_layersCache.TryGetValue(_selectedNode.Id, out var layers))
            _layersCache[_selectedNode.Id] = layers = BuildLayersForNode(_selectedNode);

        foreach (var layer in layers)
            _overlayLayers.Add(layer);
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
        var orderedAncestors = _executor.IntermediateResults.Keys
            .Where(id => ancestorIds.Contains(id))
            .Select(id => Flow.Nodes.FirstOrDefault(n => n.Id == id))
            .OfType<OperatorNodeViewModel>();

        foreach (var ancestor in orderedAncestors)
        {
            _executor.IntermediateResults.TryGetValue(ancestor.Id, out var result);
            foreach (var (port, bitmap) in ExtractImageLayers(result, ancestor.Operator.Type.OutputPorts))
            {
                var label = ancestor.Operator.Type.OutputPorts.Count > 1
                    ? $"{ancestor.DisplayName} / {port}"
                    : ancestor.DisplayName;
                layers.Add(new LayerViewModel(ancestor.Id, port, label) { Image = bitmap });
            }
        }

        _executor.IntermediateResults.TryGetValue(node.Id, out var ownResult);
        var ownImageLayers = ExtractImageLayers(ownResult, node.Operator.Type.OutputPorts).ToList();
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
        object? result, IReadOnlyList<string> outputPorts)
    {
        if (result is OpenCvSharp.Mat mat)
        {
            var bytes = ImageHelper.TryGetPngBytes(mat);
            if (bytes is not null)
                yield return (outputPorts.Count > 0 ? outputPorts[0] : "Image", BytesToBitmapSource(bytes));
            yield break;
        }
        if (result is Dictionary<string, object?> dict)
        {
            foreach (var (key, val) in dict)
            {
                var bytes = ImageHelper.TryGetPngBytes(val);
                if (bytes is not null)
                    yield return (key, BytesToBitmapSource(bytes));
            }
        }
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
