using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.UI.Dialogs;
using IPLab.UI.Services;
using System.Collections.ObjectModel;
using OperatorStatus = IPLab.Core.Models.OperatorStatus;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using IPLab.Core.Serialization;
using IPLab.Core.Utilities;
using Microsoft.Win32;
using Settings;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CoreFlow = IPLab.Core.Runtime.Flow;

namespace IPLab.UI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private FlowViewModel _flow = null!;
    public FlowViewModel Flow
    {
        get => _flow;
        private set
        {
            _flow = value;
            ThumbnailStrip.AttachToFlow(value);
            RaisePropertyChanged();
        }
    }

    private string  _savedJson       = string.Empty;
    private string? _currentFilePath = null;
    private readonly IPLabSettings _settings;

    private readonly ExecutionService   _execution;
    private readonly InspectorViewModel _inspector;

    public InspectorState                      State        => _inspector.State;
    public ObservableCollection<LayerViewModel> OverlayLayers => _inspector.OverlayLayers;

    public bool ConfirmNavigateAway()
    {
        if (SerializeCurrentFlow() == _savedJson) return true;

        var dlg = new UnsavedChangesDialog(hasSavedPath: _currentFilePath is not null)
        {
            Owner = Application.Current.MainWindow
        };
        dlg.ShowDialog();

        return dlg.Result switch
        {
            UnsavedChangesResult.Save    => SaveFlow(),
            UnsavedChangesResult.SaveAs  => SaveFlowAs(),
            UnsavedChangesResult.Discard => true,
            _                            => false
        };
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
        set { _selectedNode = value; RaisePropertyChanged(); _inspector.SetSelectedNode(value, _flow); }
    }

    public ThumbnailStripViewModel ThumbnailStrip { get; }
    public event Action<OperatorNodeViewModel?>? EditingNodeChanged;

    private OperatorNodeViewModel? _editingNode;
    public OperatorNodeViewModel? EditingNode
    {
        get => _editingNode;
        private set
        {
            _editingNode = value;
            _inspector.SetEditingNode(value);
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsSettingsPanelOpen));
            EditingNodeChanged?.Invoke(value);
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
    public ICommand         NewFlowCommand       { get; }
    public ICommand         RunOnceCommand       { get; }
    public ICommand         RunContinuousCommand { get; }
    public ICommand         StopCommand          { get; }
    public ICommand         ClearResultsCommand  { get; }
    public ICommand         CloseSettingsCommand { get; }
    public ICommand         SaveFlowCommand      { get; }
    public ICommand         LoadFlowCommand      { get; }

    public MainViewModel()
    {
        _execution = new ExecutionService();
        _inspector = new InspectorViewModel(_execution);
        _inspector.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InspectorViewModel.State))
                RaisePropertyChanged(nameof(State));
        };
        _execution.StatusChanged += OnOperatorStatusChanged;

        ThumbnailStrip = new ThumbnailStripViewModel(
            clearResults: ClearExecutionResults,
            runAsync:     async () => await ExecuteRunAsync(),
            setStatus:    s => Status = s);

        Toolbox = new ToolboxViewModel(OperatorRegistry.CreateDefault(), type => Flow.AddNode(type));

        _settings = SettingsStore.Load<IPLabSettings>();
        var lastFlow = TryLoadFlowFromPath(_settings.LastFlowPath);
        if (lastFlow is not null)
            _currentFilePath = _settings.LastFlowPath;

        Flow = CreateFlowViewModel(lastFlow ?? BuildSampleFlow());
        _savedJson = SerializeCurrentFlow();

        NewFlowCommand       = new RelayCommand(NewFlow);
        RunOnceCommand       = new RelayCommand(RunOnce, () => !IsRunningContinuous);
        RunContinuousCommand = new RelayCommand(ToggleContinuousRun);
        StopCommand          = new RelayCommand(Stop);
        ClearResultsCommand  = new RelayCommand(ClearResults);
        CloseSettingsCommand = new RelayCommand(() => EditingNode = null);
        SaveFlowCommand      = new RelayCommand(() => SaveFlowAs());
        LoadFlowCommand      = new RelayCommand(LoadFlow);
    }

    private FlowViewModel CreateFlowViewModel(IFlow flow) => new(flow,
        onOpenSettings:     node => EditingNode = (EditingNode == node) ? null : node,
        onSelected:         node => SelectedNode = node,
        onBeforeDeleteNode: node => { if (EditingNode == node) EditingNode = null;
                                      if (SelectedNode == node) SelectedNode = null; });

    private void NewFlow()
    {
        if (!ConfirmNavigateAway()) return;

        EditingNode  = null;
        SelectedNode = null;
        _execution.Clear();
        _inspector.Clear();

        Flow             = CreateFlowViewModel(new CoreFlow(new FlowDef([]), new FlowLayout([], [])));
        _currentFilePath = null;
        _savedJson       = SerializeCurrentFlow();
        Status           = "New flow";
    }

    private async void RunOnce() => await ExecuteRunAsync();

    private void ToggleContinuousRun()
    {
        if (IsRunningContinuous)
            Stop();
        else
        {
            IsRunningContinuous = true;
            RunOnce();
        }
    }

    private async Task<bool> ExecuteRunAsync()
    {
        if (ThumbnailStrip.IsEmpty)
        {
            var dialog = new OpenFileDialog
            {
                Title       = "Select image(s) to process",
                Filter      = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|All files|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog() != true) { IsRunningContinuous = false; return false; }
            ThumbnailStrip.AddPaths(dialog.FileNames);
        }

        var cycleStart = System.Diagnostics.Stopwatch.GetTimestamp();
        Status = "Running…";
        try
        {
            bool success = await _execution.RunAsync(BuildExecutionFlow());
            if (!success)
            {
                Status = "Stopped";
                return false;
            }

            await _inspector.PrecomputeAsync(Flow);
            _inspector.RefreshCachedLayerImages(Flow);

            var failed = Flow.Nodes.Count(n => n.Status == OperatorStatus.Failed);
            if (failed > 0)
            {
                Status = $"{failed} operator(s) failed";
            }
            else
            {
                var activeFile = ThumbnailStrip.ActiveFilePath;
                Status = activeFile is not null
                    ? $"Done  |  {Path.GetFileName(activeFile)}"
                    : "Done";
            }

            _inspector.UpdateSelectedImage(Flow);

            if (IsRunningContinuous)
            {
                if (failed > 0)
                    IsRunningContinuous = false;
                else
                {
                    // Enforce a minimum cycle time so a fully-cached (near-zero) run
                    // doesn't spin one CPU core at 100%.
                    const int MinCycleMs = 16; // ~60 fps ceiling
                    var elapsedMs = (System.Diagnostics.Stopwatch.GetTimestamp() - cycleStart)
                                    * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    var delayMs = (int)Math.Max(0, MinCycleMs - elapsedMs);
                    if (delayMs > 0) await Task.Delay(delayMs);
                    Application.Current?.Dispatcher.InvokeAsync(RunOnce, DispatcherPriority.Background);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
            IsRunningContinuous = false;
            return false;
        }
    }

    private void Stop()
    {
        IsRunningContinuous = false;
        _execution.Stop();
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

    private void ClearResults() => ClearExecutionResults();

    private void ClearExecutionResults()
    {
        _execution.Clear();
        _inspector.Clear();
        Status = "Ready";

        foreach (var node in Flow.Nodes)
        {
            node.Status       = OperatorStatus.NotRun;
            node.ErrorMessage = null;
        }
    }

    private string SerializeCurrentFlow()
    {
        var execFlow = BuildExecutionFlow();
        var nodeById = Flow.Nodes.ToDictionary(n => n.Id);

        var opLayouts = Flow.Nodes
            .Select(n => new OperatorLayout(n.Id, new LayoutPoint(n.Location.X, n.Location.Y)));

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

        return FlowDefSerializer.Serialize(new CoreFlow(execFlow, new FlowLayout(opLayouts, depLayouts)));
    }

    private bool SaveFlow()
    {
        if (_currentFilePath is null) return SaveFlowAs();

        var json = SerializeCurrentFlow();
        File.WriteAllText(_currentFilePath, json);
        _savedJson = json;
        _settings.LastFlowPath = _currentFilePath;
        SettingsStore.Save(_settings);
        Status = $"Saved: {Path.GetFileName(_currentFilePath)}";
        return true;
    }

    private bool SaveFlowAs()
    {
        var dialog = new SaveFileDialog
        {
            Title      = "Save Flow As",
            Filter     = "IPLab Flow|*.ipl|JSON|*.json|All files|*.*",
            DefaultExt = ".ipl"
        };
        if (dialog.ShowDialog() != true) return false;

        var json = SerializeCurrentFlow();
        File.WriteAllText(dialog.FileName, json);
        _currentFilePath = dialog.FileName;
        _savedJson       = json;
        _settings.LastFlowPath = _currentFilePath;
        SettingsStore.Save(_settings);
        Status = $"Saved: {Path.GetFileName(dialog.FileName)}";
        return true;
    }

    private void LoadFlow()
    {
        if (!ConfirmNavigateAway()) return;

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
            _execution.Clear();
            _inspector.Clear();
            Flow = CreateFlowViewModel(flow);
            _currentFilePath = dialog.FileName;
            _savedJson       = SerializeCurrentFlow();
            _settings.LastFlowPath = _currentFilePath;
            SettingsStore.Save(_settings);
            Status = $"Loaded: {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            Status = $"Load failed: {ex.Message}";
        }
    }

    private FlowDef BuildExecutionFlow() => new(
        Flow.Nodes.Select(node => new Operator
        {
            Id           = node.Operator.Id,
            DisplayName  = node.DisplayName,
            Type         = node.Operator.Type,
            Parameters   = node.Parameters.Select(p => p.ToParameterValue()).ToList(),
            Dependencies = node.Parameters
                .Where(p => p.IsWired && p.SelectedSource is not null)
                .GroupBy(p => p.SelectedSource!.OperatorId)
                .Select(g => new Dependency($"D_{g.Key}_{node.Id}", g.Key))
                .ToList()
        }));

    private static IFlow? TryLoadFlowFromPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try   { return FlowDefSerializer.Deserialize(File.ReadAllText(path), OperatorRegistry.CreateDefault()); }
        catch { return null; }
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
                Parameters   =
                [
                    new ParameterValue { Name = "FilePaths", Value = Array.Empty<string>() }
                ],
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
