using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using IPLab.Core.Interfaces;
using IPLab.Core.Models;
using IPLab.UI.Services;
using OperatorStatus = IPLab.Core.Models.OperatorStatus;

namespace IPLab.UI.ViewModels;

public class OperatorNodeViewModel : ViewModelBase
{
    public string    Id       { get; }
    public string    TypeName { get; }
    public IOperator Operator { get; }

    private string _displayName = string.Empty;
    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (_displayName == value) return;
            _displayName = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HeaderLabel));
            _onDisplayNameChanged?.Invoke(this);
        }
    }

    public string HeaderLabel => $"{Id}  {DisplayName}";

    private Point _location;
    public Point Location
    {
        get => _location;
        set { _location = value; RaisePropertyChanged(); }
    }

    // One connector per side, always visible.
    public ConnectorViewModel TopConnector    { get; } = new("Top",    ConnectionSide.Top);
    public ConnectorViewModel BottomConnector { get; } = new("Bottom", ConnectionSide.Bottom);
    public ConnectorViewModel LeftConnector   { get; } = new("Left",   ConnectionSide.Left);
    public ConnectorViewModel RightConnector  { get; } = new("Right",  ConnectionSide.Right);

    public IReadOnlyList<ParameterEditViewModel> Parameters                  { get; }
    public ICommand                              OpenSettingsCommand         { get; }
    public ICommand                              ScaffoldDebugProjectCommand { get; }
    public ICommand                              BrowseScriptCommand         { get; }
    public bool                                  IsCSharpScript             => TypeName == "CSharpScript";

    private OperatorStatus _status = OperatorStatus.NotRun;
    public OperatorStatus Status
    {
        get => _status;
        set { _status = value; RaisePropertyChanged(); }
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; RaisePropertyChanged(); }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            RaisePropertyChanged();
            if (value) _onSelected?.Invoke(this);
        }
    }

    private readonly Action<OperatorNodeViewModel>? _onSelected;
    private readonly Action<OperatorNodeViewModel>? _onDisplayNameChanged;

    public bool HasConnector(ConnectorViewModel c) =>
        TopConnector == c || BottomConnector == c || LeftConnector == c || RightConnector == c;

    public OperatorNodeViewModel(IOperator op,
                                  IEnumerable<SourceRefViewModel> availableSources,
                                  Action<OperatorNodeViewModel>? onOpenSettings = null,
                                  Action<OperatorNodeViewModel>? onSelected = null,
                                  Action<OperatorNodeViewModel>? onDisplayNameChanged = null)
    {
        Operator               = op;
        Id                     = op.Id;
        _displayName           = op.DisplayName;
        TypeName               = op.Type.TypeName;
        _onSelected            = onSelected;
        _onDisplayNameChanged  = onDisplayNameChanged;

        var sources = availableSources.ToList();
        var schemas = op.Type.ParameterSchema;
        Parameters = schemas
            .Select(schema =>
            {
                var value = op.Parameters.FirstOrDefault(p => p.Name == schema.Name);
                return new ParameterEditViewModel(schema, value, sources);
            })
            .ToList();

        // Wire conditional visibility: subscribe once after all VMs exist.
        var byName = Parameters.ToDictionary(p => p.Name);
        foreach (var (vm, schema) in Parameters.Zip(schemas))
        {
            if (schema.ShowWhenParam is not { } controllerName) continue;
            if (!byName.TryGetValue(controllerName, out var controller)) continue;

            var showValues = schema.ShowWhenValues ?? [];
            vm.IsVisible = showValues.Contains(controller.SelectedOption);

            controller.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ParameterEditViewModel.SelectedOption))
                    vm.IsVisible = showValues.Contains(controller.SelectedOption);
            };
        }

        OpenSettingsCommand         = new RelayCommand(() => onOpenSettings?.Invoke(this));
        ScaffoldDebugProjectCommand = new RelayCommand(() => CSharpScriptService.ScaffoldDebugProject(Parameters));
        BrowseScriptCommand         = new RelayCommand(() => CSharpScriptService.BrowseScript(Parameters));
    }
}
