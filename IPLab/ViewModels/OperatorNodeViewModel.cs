using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using IPLab.Core.Interfaces;

namespace IPLab.ViewModels;

public class OperatorNodeViewModel : ViewModelBase
{
    public string    Id          { get; }
    public string    DisplayName { get; }
    public string    TypeName    { get; }
    public IOperator Operator    { get; }

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

    public IReadOnlyList<ParameterEditViewModel> Parameters { get; }
    public ICommand                              OpenSettingsCommand { get; }

    public bool HasConnector(ConnectorViewModel c) =>
        TopConnector == c || BottomConnector == c || LeftConnector == c || RightConnector == c;

    public OperatorNodeViewModel(IOperator op,
                                  IEnumerable<SourceRefViewModel> availableSources,
                                  Action<OperatorNodeViewModel>? onOpenSettings = null)
    {
        Operator    = op;
        Id          = op.Id;
        DisplayName = op.DisplayName;
        TypeName    = op.Type.TypeName;

        var sources = availableSources.ToList();
        Parameters = op.Type.ParameterSchema
            .Select(schema =>
            {
                var value = op.Parameters.FirstOrDefault(p => p.Name == schema.Name);
                return new ParameterEditViewModel(schema, value, sources);
            })
            .ToList();

        OpenSettingsCommand = new RelayCommand(() => onOpenSettings?.Invoke(this));
    }
}
