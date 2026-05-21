using IPLab.Core.Interfaces;
using IPLab.Core.Models;
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

namespace IPLab.ViewModels;

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

    private BitmapSource? _selectedImage;
    public BitmapSource? SelectedImage
    {
        get => _selectedImage;
        private set { _selectedImage = value; RaisePropertyChanged(); }
    }

    private CircleSegment[]? _selectedCircles;
    public CircleSegment[]? SelectedCircles
    {
        get => _selectedCircles;
        private set { _selectedCircles = value; RaisePropertyChanged(); }
    }

    private KeyPoint[]? _selectedBlobs;
    public KeyPoint[]? SelectedBlobs
    {
        get => _selectedBlobs;
        private set { _selectedBlobs = value; RaisePropertyChanged(); }
    }

    private ConnectedComponentInfo[]? _selectedComponents;
    public ConnectedComponentInfo[]? SelectedComponents
    {
        get => _selectedComponents;
        private set { _selectedComponents = value; RaisePropertyChanged(); }
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

    private FlowEx? _executor;

    public ToolboxViewModel Toolbox      { get; }
    public ICommand RunAllCommand        { get; }
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
        try
        {
            var flow = BuildExecutionFlow();
            _executor = new FlowEx(flow);
            _executor.StatusChanged += OnOperatorStatusChanged;
            await _executor.RunAllAsync();

            var failed = Flow.Nodes.Count(n => n.Status == OperatorStatus.Failed);
            Status = failed > 0
                ? $"{failed} operator(s) failed"
                : $"Done  |  {Path.GetFileName(filePathParam?.ValueText ?? string.Empty)}";
            UpdateSelectedImage();
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
    }

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
        _executor          = null;
        SelectedImage      = null;
        SelectedComponents = null;
        Status             = "Ready";

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

            EditingNode   = null;
            _executor     = null;
            SelectedImage = null;
            Flow   = new FlowViewModel(flow,
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
            SelectedImage   = null;
            SelectedCircles = null;
            SelectedBlobs   = null;
            return;
        }

        _executor.IntermediateResults.TryGetValue(_selectedNode.Id, out var result);

        if (result is CircleSegment[] circles)
        {
            SelectedImage      = GetSourceImage();
            SelectedCircles    = circles;
            SelectedBlobs      = null;
            SelectedComponents = null;
            return;
        }

        if (result is KeyPoint[] blobs)
        {
            SelectedImage      = GetSourceImage();
            SelectedBlobs      = blobs;
            SelectedCircles    = null;
            SelectedComponents = null;
            return;
        }

        if (result is ConnectedComponentInfo[] components)
        {
            SelectedImage      = GetSourceImage();
            SelectedComponents = components;
            SelectedCircles    = null;
            SelectedBlobs      = null;
            return;
        }

        SelectedCircles    = null;
        SelectedBlobs      = null;
        SelectedComponents = null;
        var bytes = ImageHelper.TryGetPngBytes(result);
        SelectedImage = bytes is not null ? BytesToBitmapSource(bytes) : null;
    }

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
